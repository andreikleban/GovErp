using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

/// <summary>
/// A catalog rule: it reads the snapshot and writes outcomes.
/// </summary>
public interface IValidationRule
{
    string RuleId { get; }

    /// <summary>
    /// The numeric parameters the rule reads from its definition, with their meaning.
    /// </summary>
    IReadOnlyList<ParameterSpec> Parameters { get; }

    IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition);
}
