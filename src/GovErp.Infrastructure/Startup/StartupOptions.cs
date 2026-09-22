namespace GovErp.Infrastructure.Startup;

/// <summary>
/// Секция "Startup": сведения и учётные данные, нужные до появления runtime-пользователей тенантов.
/// MigrationConnectionTemplate: "…;Database={0};User Id=sa;Password=…" — форматируется именем БД (или "master").
/// </summary>
public sealed class StartupOptions
{
    public string MigrationConnectionTemplate { get; set; } = "";
    public string MasterDatabase { get; set; } = "GovErp_Master";
    public string MasterRuntimeLogin { get; set; } = "";
    public string MasterRuntimePassword { get; set; } = "";
    /// <summary>Имена БД тенантов, которые разрешено сбрасывать через demo-reset (spec §5, правило 10).</summary>
    public string[] DemoResetAllowlist { get; set; } = [];
}
