using GovErp.Application.Web.Approvals;
using GovErp.Application.Web.Audit;
using GovErp.Application.Web.Budget;
using GovErp.Application.Web.Explanation;
using GovErp.Application.Web.Extensions;
using GovErp.Application.Web.Invoices;
using GovErp.Application.Web.Ledger;
using GovErp.Application.Web.Posting;
using GovErp.Application.Web.Purchasing;
using GovErp.Application.Web.Reference;
using GovErp.Application.Web.Tenancy;
using GovErp.Infrastructure.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GovErp.Application.Web.Tests;

/// <summary>The AddApplication + AddInfrastructure dependency graph builds without a database: catches unregistered ports and scoped services captured by singletons.</summary>
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
        sp.GetRequiredService<IPurchasingAppService>().Should().NotBeNull();
        sp.GetRequiredService<ILedgerAppService>().Should().NotBeNull();
        sp.GetRequiredService<IReferenceAppService>().Should().NotBeNull();
        sp.GetRequiredService<IAuditAppService>().Should().NotBeNull();
        sp.GetRequiredService<ITenantUserDirectory>().Should().NotBeNull();
        sp.GetRequiredService<IExplanationAppService>().Should().NotBeNull();

        // As in the runner: the tenant DbContext is built only after the tenant context is initialized (no database connection).
        sp.GetRequiredService<ITenantContextInitializer>().Initialize(new TenantId("demo"), "Server=.;Database=GovErpDemo");
        sp.GetRequiredService<InvoiceWorkspace>().Should().NotBeNull();
    }
}
