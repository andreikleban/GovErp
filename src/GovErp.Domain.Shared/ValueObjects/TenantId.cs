namespace GovErp.Domain.Shared.ValueObjects;

/// <summary>
/// Identifier of a tenant.
/// </summary>
public sealed record TenantId
{
    public string Value { get; }
    public TenantId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }
    public override string ToString() => Value;
}
