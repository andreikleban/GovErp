namespace GovErp.Application.Web.Tenancy;

public interface ITenantContextInitializer
{
    void Initialize(TenantId tenantId, string connectionString);
}
