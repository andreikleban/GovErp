using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.Exceptions;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.RuleSupport;

/// <summary>
/// Parameters of the rule version in effect, read through the rule's own ParameterSpec. A missing or invalid value is a
/// configuration error: the evaluation is refused instead of silently skipping the check.
/// </summary>
public sealed class RuleParameters
{
    private readonly RuleDefinition _definition;

    internal RuleParameters(RuleDefinition definition) => _definition = definition;

    public decimal Read(ParameterSpec spec)
    {
        var value = _definition.DecimalParameter(spec.Name);
        if (!spec.Accepts(value))
        {
            throw new ValidationException(spec.InvalidValueCode, ("rule", _definition.RuleId), ("parameter", spec.Name), ("value", value));
        }

        return value;
    }
}
