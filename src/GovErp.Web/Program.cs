using GovErp.Application.Web.Extensions;
using GovErp.Infrastructure.Extensions;
using GovErp.Infrastructure.Startup;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApplication(builder.Configuration).AddInfrastructure(builder.Configuration).AddStartup(builder.Configuration);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();
if (args.FirstOrDefault() == "demo-reset")
{
    return await DemoReset.RunAsync(args, app.Services, app.Environment, CancellationToken.None);
}

await DatabaseInitializer.InitializeAsync(app.Services, CancellationToken.None);
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<GovErp.Web.Components.App>().AddInteractiveServerRenderMode();
await app.RunAsync();
return 0;
