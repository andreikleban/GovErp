using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

public interface IValidationRule
{
    string RuleId { get; }

    /// <summary>The numeric parameters the rule reads from its definition, with their meaning.</summary>
    IReadOnlyList<ParameterSpec> Parameters { get; }

    IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition);
}
