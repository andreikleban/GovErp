using GovErp.Application.Web.Audit;
using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Explanation;
using GovErp.Application.Web.Identity;
using GovErp.Application.Web.Invoices;
using GovErp.Application.Web.Tenancy;
using GovErp.Domain.ChartOfAccounts.Repositories;
using GovErp.Domain.Ledger.Repositories;
using GovErp.Domain.Payables.Repositories;
using GovErp.Domain.Validation.Repositories;
using GovErp.Infrastructure.Audit;
using GovErp.Infrastructure.Common;
using GovErp.Infrastructure.Explanation;
using GovErp.Infrastructure.Identity;
using GovErp.Infrastructure.Master;
using GovErp.Infrastructure.Operations;
using GovErp.Infrastructure.Persistence;
using GovErp.Infrastructure.Persistence.Repositories;
using GovErp.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GovErp.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// EnableRetryOnFailure is not enabled: the EF retry strategy is incompatible with an explicit user transaction,
    /// and the runner retries the whole command.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection s, IConfiguration cfg)
    {
        s.Configure<TenancyOptions>(cfg.GetSection("Tenancy"));
        s.Configure<ClockOptions>(cfg.GetSection("Clock"));
        s.AddDbContext<MasterDbContext>(o => o.UseSqlServer(cfg.GetConnectionString("Master")));
        s.AddScoped<TenantContext>();
        s.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        s.AddScoped<ITenantContextInitializer>(sp => sp.GetRequiredService<TenantContext>());
        s.AddSingleton<AppendOnlyInterceptor>();
        s.AddDbContext<GovErpDbContext>((sp, o) => o
            .UseSqlServer(sp.GetRequiredService<ITenantContext>().ConnectionString)   // no EnableRetryOnFailure: retries are the runner's job
            .AddInterceptors(sp.GetRequiredService<AppendOnlyInterceptor>()));
        s.AddSingleton<ITenantOperationRunner, EfTenantOperationRunner>();
        s.AddScoped<ICommandReceipts, EfCommandReceipts>();
        s.AddScoped<IConcurrencyGuard, EfConcurrencyGuard>();
        s.AddScoped<IFundRepository, EfFundRepository>();
        s.AddScoped<IGrantRepository, EfGrantRepository>();
        s.AddScoped<IAccountCombinationRepository, EfAccountCombinationRepository>();
        s.AddScoped<IReferenceDataRepository, EfReferenceDataRepository>();
        s.AddScoped<IOpeningBalanceRepository, EfOpeningBalanceRepository>();
        s.AddScoped<IBudgetLineRepository, EfBudgetLineRepository>();
        s.AddScoped<IEncumbranceRepository, EfEncumbranceRepository>();
        s.AddScoped<IJournalRepository, EfJournalRepository>();
        s.AddScoped<IFiscalPeriodRepository, EfFiscalPeriodRepository>();
        s.AddScoped<IVendorRepository, EfVendorRepository>();
        s.AddScoped<IPurchaseOrderRepository, EfPurchaseOrderRepository>();
        s.AddScoped<IVendorInvoiceRepository, EfVendorInvoiceRepository>();
        s.AddScoped<IInvoiceNumbering, EfInvoiceNumbering>();
        s.AddScoped<IRuleDefinitionRepository, EfRuleDefinitionRepository>();
        s.AddScoped<IEvaluationRecordRepository, EfEvaluationRecordRepository>();
        s.AddScoped<IAuditTrail, EfAuditTrail>();
        s.AddScoped<IExplanationRepository, EfExplanationRepository>();
        s.AddScoped<ITenantCatalog, MasterTenantCatalog>();
        s.AddScoped<ITenantUserDirectory, MasterTenantUserDirectory>();
        s.AddScoped<ISignIn, MasterSignIn>();
        s.AddSingleton<IPasswordHasher<UserAccount>, PasswordHasher<UserAccount>>();
        s.AddSingleton<IClock, SystemClock>();
        s.AddExplanation(cfg);
        return s;
    }
}
