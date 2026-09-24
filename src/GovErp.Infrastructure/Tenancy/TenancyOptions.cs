namespace GovErp.Infrastructure.Tenancy;

/// <summary>
/// The "Tenancy" section. RuntimeConnectionTemplate: "Server=…;Database={0};User Id={1};Password={2};TrustServerCertificate=True";
/// MigrationConnectionTemplate: "Server=…;Database={0};User Id=sa;Password=…;TrustServerCertificate=True";
/// Credentials: the tenant's CredentialKey → credentials of the runtime user of its database.
/// </summary>
public sealed class TenancyOptions
{
    public string RuntimeConnectionTemplate { get; set; } = "";
    public string MigrationConnectionTemplate { get; set; } = "";
    public Dictionary<string, TenantCredential> Credentials { get; set; } = [];
}

public sealed class TenantCredential
{
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
}
