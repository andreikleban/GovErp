using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

public interface IValidationRule
{
    string RuleId { get; }
    IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition);
}
