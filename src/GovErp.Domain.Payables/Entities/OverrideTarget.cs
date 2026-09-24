namespace GovErp.Domain.Payables.Entities;

/// <summary>
/// Which evaluation outcome an override releases: rule, line and cycle.
/// </summary>
public sealed record OverrideTarget(Guid EvaluationRef, Guid OutcomeRef, string RuleId, int RuleVersion, int? DistributionLine, int ContentVersion, Guid ApprovalCycleId);
