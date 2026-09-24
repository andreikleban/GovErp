namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>Whether a pipeline step ran: a skipped check is never shown as passed.</summary>
public sealed record StepExecution(ValidationStep Step, StepExecutionStatus Status);
