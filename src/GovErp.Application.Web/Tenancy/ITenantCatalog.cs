namespace GovErp.Application.Web.Tenancy;

/// <summary>
/// Registry of tenants and their connection strings.
/// </summary>
public interface ITenantCatalog
{
    Task<TenantInfo?> FindAsync(TenantId id, CancellationToken ct = default);
    Task<IReadOnlyList<TenantInfo>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// Connection string of the tenant's runtime user; the secret does not leave Infrastructure.
    /// </summary>
    string RuntimeConnectionString(TenantInfo tenant);
}
