namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>
/// Rule layer, from lowest to highest: Core, Federal, State, Tenant.
/// </summary>
public enum RuleLayer { Core, Federal, State, Tenant }
