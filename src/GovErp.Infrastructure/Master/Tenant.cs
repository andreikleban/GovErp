namespace GovErp.Infrastructure.Master;

/// <summary>A tenant catalog entry. An anemic infrastructure model, not domain (DDD-7).</summary>
public sealed class Tenant
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string DatabaseName { get; set; } = "";
    /// <summary>Key of the configuration section with the runtime user's credentials; the secret itself is not stored in the database.</summary>
    public string CredentialKey { get; set; } = "";
    public bool IsDemo { get; set; }
}
