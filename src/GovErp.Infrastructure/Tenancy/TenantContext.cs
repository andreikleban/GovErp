using GovErp.Application.Web.Tenancy;

namespace GovErp.Infrastructure.Tenancy;

public sealed class TenantContext : ITenantContext, ITenantContextInitializer
{
    private TenantId? _tenantId;
    private string? _connectionString;

    public TenantId TenantId => _tenantId ?? throw new InvalidOperationException("Tenant context is not initialized.");
    public string ConnectionString => _connectionString ?? throw new InvalidOperationException("Tenant context is not initialized.");
    public bool IsInitialized => _tenantId is not null;

    /// <summary>Once per operation scope; re-initializing with another tenant is an error.</summary>
    public void Initialize(TenantId tenantId, string connectionString)
    {
        if (_tenantId is not null && _tenantId != tenantId)
        {
            throw new InvalidOperationException("Tenant context is already initialized for another tenant.");
        }

        _tenantId = tenantId;
        _connectionString = connectionString;
    }
}
