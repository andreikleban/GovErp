namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>Выполнялся ли шаг конвейера: пропущенная проверка не изображается успешной.</summary>
public sealed record StepExecution(ValidationStep Step, StepExecutionStatus Status);
