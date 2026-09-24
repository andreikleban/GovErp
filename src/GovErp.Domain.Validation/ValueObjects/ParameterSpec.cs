using GovErp.Domain.Validation.Exceptions;

namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>
/// Kind of a rule parameter: an amount, or a share from 0 to 1.
/// </summary>
public enum ParameterKind { Amount, Share }

/// <summary>
/// Which direction of a parameter makes its rule stricter.
/// </summary>
public enum Stricter { WhenLower, WhenHigher }

/// <summary>
/// A numeric parameter as its rule understands it: the valid values and which direction tightens the rule.
/// Declared by the rule itself, so the override guard, the rule and the New version form read the same meaning.
/// </summary>
public sealed record ParameterSpec(string Name, ParameterKind Kind, Stricter Direction)
{
    /// <summary>
    /// A positive amount, for example a threshold in USD.
    /// </summary>
    public static ParameterSpec Amount(string name, Stricter direction) => new(name, ParameterKind.Amount, direction);

    /// <summary>
    /// A share from 0 to 1 inclusive (0.05 means 5%).
    /// </summary>
    public static ParameterSpec Share(string name, Stricter direction) => new(name, ParameterKind.Share, direction);

    /// <summary>
    /// The problem code when a value is not valid for this parameter.
    /// </summary>
    public string InvalidValueCode => Kind == ParameterKind.Amount ? ValidationErrors.ParameterNotPositiveAmount : ValidationErrors.ParameterNotShare;

    public bool Accepts(decimal value) => Kind == ParameterKind.Amount ? value > 0 : value is >= 0 and <= 1;

    /// <summary>
    /// A more local layer's value is valid and at least as strict as the value of the layer above it.
    /// </summary>
    public bool KeepsGuard(decimal upper, decimal local) =>
        Accepts(local) && (Direction == Stricter.WhenLower ? local <= upper : local >= upper);
}
