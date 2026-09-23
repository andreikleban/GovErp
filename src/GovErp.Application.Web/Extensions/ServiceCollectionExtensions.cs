using GovErp.Application.Web.Approvals;
using GovErp.Application.Web.Audit;
using GovErp.Application.Web.Budget;
using GovErp.Application.Web.Explanation;
using GovErp.Application.Web.Invoices;
using GovErp.Application.Web.Ledger;
using GovErp.Application.Web.Posting;
using GovErp.Application.Web.Purchasing;
using GovErp.Application.Web.Reference;
using GovErp.Application.Web.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GovErp.Application.Web.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection s, IConfiguration cfg)
    {
        s.Configure<PostingOptions>(cfg.GetSection("Posting"));
        s.AddScoped<ValidationSubjectAssembler>();
        s.AddScoped<InvoiceWorkspace>();
        s.AddScoped<IInvoiceAppService, InvoiceAppService>();
        s.AddScoped<IApprovalAppService, ApprovalAppService>();
        s.AddScoped<IPostingAppService, PostingAppService>();
        s.AddScoped<IBudgetAppService, BudgetAppService>();
        s.AddScoped<IPurchasingAppService, PurchasingAppService>();
        s.AddScoped<ILedgerAppService, LedgerAppService>();
        s.AddScoped<IReferenceAppService, ReferenceAppService>();
        s.AddScoped<IAuditAppService, AuditAppService>();
        s.AddScoped<IExplanationAppService, ExplanationAppService>();
        return s;
    }
}
