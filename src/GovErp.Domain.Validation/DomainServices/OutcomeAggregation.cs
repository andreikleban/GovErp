using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

public static class OutcomeAggregation
{
    public static (IReadOnlyList<RuleOutcome> WithOverrides, Severity Overall) Apply(
        IReadOnlyList<RuleOutcome> outcomes, ValidationSubject subject, RuleSetVersions currentVersions) =>
        throw new NotImplementedException();
}
