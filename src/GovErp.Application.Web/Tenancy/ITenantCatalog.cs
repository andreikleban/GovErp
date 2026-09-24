namespace GovErp.Application.Web.Tenancy;

public interface ITenantCatalog
{
    Task<TenantInfo?> FindAsync(TenantId id, CancellationToken ct = default);
    Task<IReadOnlyList<TenantInfo>> ListAsync(CancellationToken ct = default);

    /// <summary>Connection string of the tenant's runtime user; the secret does not leave Infrastructure.</summary>
    string RuntimeConnectionString(TenantInfo tenant);
}
