namespace GovErp.Application.Web.Tenancy;

public sealed record TenantInfo(TenantId Id, string Name, string DatabaseName, bool IsDemo);
