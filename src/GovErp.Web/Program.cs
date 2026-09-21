var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();
app.UseAntiforgery();
app.MapRazorComponents<GovErp.Web.Components.App>().AddInteractiveServerRenderMode();
app.Run();
