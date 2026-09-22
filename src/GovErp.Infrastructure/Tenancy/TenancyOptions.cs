namespace GovErp.Infrastructure.Tenancy;

/// <summary>
/// Секция "Tenancy". RuntimeConnectionTemplate: "Server=…;Database={0};User Id={1};Password={2};TrustServerCertificate=True";
/// MigrationConnectionTemplate: "Server=…;Database={0};User Id=sa;Password=…;TrustServerCertificate=True";
/// Credentials: CredentialKey тенанта → учётные данные runtime-пользователя его БД.
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
