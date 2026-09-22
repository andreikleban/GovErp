namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>Outcome'ы оценки, на которую ссылается действующий override: доказательство, против которого он выдан.</summary>
public sealed record PreviousEvaluation(Guid EvaluationId, IReadOnlyList<RuleOutcome> Outcomes);
