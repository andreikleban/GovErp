namespace GovErp.Domain.Payables.Entities;

public sealed record OverrideTarget(Guid EvaluationRef, Guid OutcomeRef, string RuleId, int RuleVersion, int? DistributionLine, int ContentVersion, Guid ApprovalCycleId);
