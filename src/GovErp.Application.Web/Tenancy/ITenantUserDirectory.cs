namespace GovErp.Application.Web.Tenancy;

/// <summary>Users of the current tenant from Master (the runtime connection to Master is read-only, which does not affect reads).</summary>
public interface ITenantUserDirectory
{
    Task<IReadOnlyList<TenantUserRecord>> ListAsync(TenantId tenantId, CancellationToken ct = default);
}
