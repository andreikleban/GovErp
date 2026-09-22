namespace GovErp.Application.Web.Tenancy;

public interface ITenantContext
{
    TenantId TenantId { get; }
    string ConnectionString { get; }
    bool IsInitialized { get; }
}
