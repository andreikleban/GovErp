using System.Globalization;
using GovErp.Application.Web.Extensions;
using GovErp.Infrastructure.Extensions;
using GovErp.Infrastructure.Startup;
using GovErp.Web.Authentication;
using GovErp.Web.Components;
using GovErp.Web.Extensions;

var unitedStates = CultureInfo.GetCultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = unitedStates;
CultureInfo.DefaultThreadCurrentUICulture = unitedStates;

var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsEnvironment("Demo"))
{
    // Статические ресурсы Blazor, включая blazor.web.js, подключаются сами только в Development.
    // Aspire поднимает Web в среде Demo, и без этого скрипта кнопки на странице не получают события.
    builder.WebHost.UseStaticWebAssets();
}

AspireSqlConfiguration.Apply(builder.Configuration);
builder.Services.AddApplication(builder.Configuration).AddInfrastructure(builder.Configuration).AddStartup(builder.Configuration);
builder.Services.AddWebHost(builder.Configuration);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();
if (args.FirstOrDefault() == "demo-reset")
{
    return await DemoReset.RunAsync(args, app.Services, app.Environment, CancellationToken.None);
}

await DatabaseInitializer.InitializeAsync(app.Services, CancellationToken.None);
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("en-US")
    .AddSupportedCultures("en-US")
    .AddSupportedUICultures("en-US"));
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapAuth();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
await app.RunAsync();
return 0;

public partial class Program;
