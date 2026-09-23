using GovErp.Application.Web.Audit;
using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Explanation;
using GovErp.Domain.ChartOfAccounts.Entities;
using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Payables.Entities;
using GovErp.Domain.Validation.Entities;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Infrastructure.Persistence;

public sealed class GovErpDbContext(DbContextOptions<GovErpDbContext> options) : DbContext(options)
{
    public DbSet<Fund> Funds => Set<Fund>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<ObjectCodeDefinition> ObjectCodes => Set<ObjectCodeDefinition>();
    public DbSet<Grant> Grants => Set<Grant>();
    public DbSet<AccountCombination> AccountCombinations => Set<AccountCombination>();
    public DbSet<OpeningBalance> OpeningBalances => Set<OpeningBalance>();
    public DbSet<BudgetLine> BudgetLines => Set<BudgetLine>();
    public DbSet<Encumbrance> Encumbrances => Set<Encumbrance>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<FiscalPeriod> FiscalPeriods => Set<FiscalPeriod>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<VendorInvoice> VendorInvoices => Set<VendorInvoice>();
    public DbSet<RuleDefinition> RuleDefinitions => Set<RuleDefinition>();
    public DbSet<EvaluationRecord> EvaluationRecords => Set<EvaluationRecord>();
    public DbSet<ExplanationRecord> Explanations => Set<ExplanationRecord>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<CommandReceipt> CommandReceipts => Set<CommandReceipt>();
    public DbSet<DocumentCounter> DocumentCounters => Set<DocumentCounter>();

    protected override void OnModelCreating(ModelBuilder b) =>
        b.ApplyConfigurationsFromAssembly(typeof(GovErpDbContext).Assembly,
            t => t.Namespace?.StartsWith("GovErp.Infrastructure.Persistence.Configurations", StringComparison.Ordinal) == true);
}
