namespace GovErp.Infrastructure.Startup;

/// <summary>
/// The "Startup" section: settings and credentials needed before the tenant runtime users exist.
/// MigrationConnectionTemplate: "…;Database={0};User Id=sa;Password=…", formatted with the database name (or "master").
/// </summary>
public sealed class StartupOptions
{
    public string MigrationConnectionTemplate { get; set; } = "";
    public string MasterDatabase { get; set; } = "GovErp_Master";
    public string MasterRuntimeLogin { get; set; } = "";
    public string MasterRuntimePassword { get; set; } = "";
    /// <summary>Names of tenant databases that may be reset via demo-reset (spec §5, rule 10).</summary>
    public string[] DemoResetAllowlist { get; set; } = [];
}
