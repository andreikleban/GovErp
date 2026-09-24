namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>
/// Outcomes of the evaluation referenced by an active override: the evidence it was granted against.
/// </summary>
public sealed record PreviousEvaluation(Guid EvaluationId, IReadOnlyList<RuleOutcome> Outcomes);
