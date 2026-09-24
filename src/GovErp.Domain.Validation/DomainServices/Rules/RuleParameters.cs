using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.Exceptions;

namespace GovErp.Domain.Validation.DomainServices.Rules;

/// <summary>
/// Parameters of the rule version in effect. A missing or invalid value is a configuration error: the evaluation
/// is refused instead of silently skipping the check.
/// </summary>
public sealed class RuleParameters
{
    private readonly RuleDefinition _definition;

    internal RuleParameters(RuleDefinition definition) => _definition = definition;

    public decimal Number(string name) => _definition.DecimalParameter(name);

    public Money PositiveAmount(string name)
    {
        var amount = Money.Of(Number(name));
        if (amount <= Money.Zero)
        {
            throw new ValidationException($"Rule {_definition.RuleId}: {name} must be positive.");
        }

        return amount;
    }

    /// <summary>A share from 0 to 1 inclusive (0.05 means 5%).</summary>
    public decimal Share(string name)
    {
        var share = Number(name);
        if (share is < 0 or > 1)
        {
            throw new ValidationException($"Rule {_definition.RuleId}: {name} must be between 0 and 1 inclusive.");
        }

        return share;
    }
}
