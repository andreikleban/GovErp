namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>
/// Severity, from lowest to highest: Allowed, Warning, SoftStop, HardStop.
/// </summary>
public enum Severity { Allowed = 0, Warning = 1, SoftStop = 2, HardStop = 3 }
