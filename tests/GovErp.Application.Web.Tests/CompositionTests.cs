using GovErp.Application.Web.Approvals;
using GovErp.Application.Web.Budget;
using GovErp.Application.Web.Explanation;
using GovErp.Application.Web.Extensions;
using GovErp.Application.Web.Invoices;
using GovErp.Application.Web.Posting;
using GovErp.Application.Web.Reference;
using GovErp.Application.Web.Tenancy;
using GovErp.Infrastructure.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GovErp.Application.Web.Tests;

/// <summary>Граф зависимостей AddApplication + AddInfrastructure собирается без БД: ловит незарегистрированные порты и захват scoped из singleton.</summary>
public class CompositionTests
{
    [Fact]
    public void Application_and_infrastructure_compose_with_validated_scopes()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Master"] = "Server=.;Database=GovErpMaster",
        }).Build();
        var services = new ServiceCollection().AddLogging().AddApplication(cfg).AddInfrastructure(cfg);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        var sp = scope.ServiceProvider;
        sp.GetRequiredService<IInvoiceAppService>().Should().NotBeNull();
        sp.GetRequiredService<IApprovalAppService>().Should().NotBeNull();
        sp.GetRequiredService<IPostingAppService>().Should().NotBeNull();
        sp.GetRequiredService<IBudgetAppService>().Should().NotBeNull();
        sp.GetRequiredService<IReferenceAppService>().Should().NotBeNull();
        sp.GetRequiredService<IExplanationAppService>().Should().NotBeNull();

        // Как в runner'е: DbContext тенанта строится только после инициализации контекста тенанта (подключения к БД нет).
        sp.GetRequiredService<ITenantContextInitializer>().Initialize(new TenantId("demo"), "Server=.;Database=GovErpDemo");
        sp.GetRequiredService<InvoiceWorkspace>().Should().NotBeNull();
    }
}
