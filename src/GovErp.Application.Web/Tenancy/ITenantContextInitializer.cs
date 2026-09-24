namespace GovErp.Application.Web.Tenancy;

/// <summary>
/// Sets the tenant on the current operation scope.
/// </summary>
public interface ITenantContextInitializer
{
    void Initialize(TenantId tenantId, string connectionString);
}
