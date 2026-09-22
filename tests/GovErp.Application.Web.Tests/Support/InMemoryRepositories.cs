using GovErp.Domain.ChartOfAccounts.Entities;
using GovErp.Domain.ChartOfAccounts.Repositories;
using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Ledger.Repositories;
using GovErp.Domain.Payables.Entities;
using GovErp.Domain.Payables.Repositories;
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.Repositories;

namespace GovErp.Application.Web.Tests;

// Фейки над List<T>, по одному классу на порт (src/GovErp.Domain.*/Repositories). Никакой персистентности:
// тесты видят те же экземпляры, что были переданы в конструктор.

public sealed class InMemoryFundRepository(IEnumerable<Fund> items) : IFundRepository
{
    private readonly List<Fund> Items = [.. items];
    public Task<Fund?> FindAsync(FundCode code, CancellationToken ct = default) =>
        Task.FromResult(Items.SingleOrDefault(f => f.Code == code));
    public Task<IReadOnlyList<Fund>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Fund>>(Items.AsReadOnly());
}

public sealed class InMemoryGrantRepository(IEnumerable<Grant> items) : IGrantRepository
{
    private readonly List<Grant> Items = [.. items];
    public Task<Grant?> FindAsync(GrantCode code, CancellationToken ct = default) =>
        Task.FromResult(Items.SingleOrDefault(g => g.Code == code));
    public Task<IReadOnlyList<Grant>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Grant>>(Items.AsReadOnly());
}

public sealed class InMemoryAccountCombinationRepository(IEnumerable<AccountCombination> items) : IAccountCombinationRepository
{
    private readonly List<AccountCombination> Items = [.. items];
    public Task<AccountCombination?> FindAsync(AccountCode code, CancellationToken ct = default) =>
        Task.FromResult(Items.SingleOrDefault(c => c.Code == code));
    public Task<IReadOnlyList<AccountCombination>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AccountCombination>>(Items.AsReadOnly());
    public Task AddAsync(AccountCombination combination, CancellationToken ct = default)
    {
        Items.Add(combination);
        return Task.CompletedTask;
    }
}

/// <summary>Один порт над двумя справочниками (Department, ObjectCodeDefinition) — конструктор берёт обе коллекции.</summary>
public sealed class InMemoryReferenceDataRepository(IEnumerable<Department> departments, IEnumerable<ObjectCodeDefinition> objects) : IReferenceDataRepository
{
    private readonly List<Department> Departments = [.. departments];
    private readonly List<ObjectCodeDefinition> Objects = [.. objects];
    public Task<Department?> FindDepartmentAsync(DepartmentCode code, CancellationToken ct = default) =>
        Task.FromResult(Departments.SingleOrDefault(d => d.Code == code));
    public Task<ObjectCodeDefinition?> FindObjectAsync(ObjectCode code, CancellationToken ct = default) =>
        Task.FromResult(Objects.SingleOrDefault(o => o.Code == code));
    public Task<IReadOnlyList<Department>> ListDepartmentsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Department>>(Departments.AsReadOnly());
    public Task<IReadOnlyList<ObjectCodeDefinition>> ListObjectsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ObjectCodeDefinition>>(Objects.AsReadOnly());
}

public sealed class InMemoryBudgetLineRepository(IEnumerable<BudgetLine> items) : IBudgetLineRepository
{
    private readonly List<BudgetLine> Items = [.. items];
    public Task<BudgetLine?> FindAsync(AccountCode account, FiscalYear fiscalYear, CancellationToken ct = default) =>
        Task.FromResult(Items.SingleOrDefault(l => l.Account == account && l.FiscalYear == fiscalYear));
    public Task<IReadOnlyList<BudgetLine>> ListAsync(FiscalYear fiscalYear, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<BudgetLine>>(Items.Where(l => l.FiscalYear == fiscalYear).ToList());
    public Task<BudgetLine?> FindByReservationAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Items.SingleOrDefault(l => l.Reservations.Any(r => r.Id == id)));
    public Task AddAsync(BudgetLine line, CancellationToken ct = default)
    {
        Items.Add(line);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryEncumbranceRepository(IEnumerable<Encumbrance> items) : IEncumbranceRepository
{
    private readonly List<Encumbrance> Items = [.. items];
    public Task<Encumbrance?> FindByPoLineAsync(string poLineRef, CancellationToken ct = default) =>
        Task.FromResult(Items.SingleOrDefault(e => e.PoLineRef == poLineRef));
    public Task<Encumbrance?> FindByClaimAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Items.SingleOrDefault(e => e.Claims.Any(c => c.Id == id) || e.BillingClaims.Any(c => c.Id == id)));
    public Task AddAsync(Encumbrance encumbrance, CancellationToken ct = default)
    {
        Items.Add(encumbrance);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryFiscalPeriodRepository(IEnumerable<FiscalPeriod> items) : IFiscalPeriodRepository
{
    private readonly List<FiscalPeriod> Items = [.. items];
    public Task<FiscalPeriod?> FindAsync(int year, int month, CancellationToken ct = default) =>
        Task.FromResult(Items.SingleOrDefault(p => p.Year == year && p.Month == month));
}

public sealed class InMemoryOpeningBalanceRepository(IEnumerable<OpeningBalance> items) : IOpeningBalanceRepository
{
    private readonly List<OpeningBalance> Items = [.. items];
    public Task<IReadOnlyList<OpeningBalance>> ListAsync(FiscalYear fiscalYear, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<OpeningBalance>>(Items.Where(o => o.FiscalYear == fiscalYear).ToList());
    public Task AddAsync(OpeningBalance balance, CancellationToken ct = default)
    {
        Items.Add(balance);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryJournalRepository(IEnumerable<JournalEntry> items) : IJournalRepository
{
    private readonly List<JournalEntry> Items = [.. items];
    public Task AddAsync(JournalEntry entry, CancellationToken ct = default)
    {
        Items.Add(entry);
        return Task.CompletedTask;
    }
    public Task<IReadOnlyList<JournalEntry>> ListBySourceAsync(string sourceRef, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<JournalEntry>>(Items.Where(j => j.SourceRef == sourceRef).ToList());
}

public sealed class InMemoryPurchaseOrderRepository(IEnumerable<PurchaseOrder> items) : IPurchaseOrderRepository
{
    private readonly List<PurchaseOrder> Items = [.. items];
    public Task<PurchaseOrder?> FindByNumberAsync(string number, CancellationToken ct = default) =>
        Task.FromResult(Items.SingleOrDefault(p => p.Number == number));
    public Task<IReadOnlyList<PurchaseOrder>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PurchaseOrder>>(Items.AsReadOnly());
}

public sealed class InMemoryVendorRepository(IEnumerable<Vendor> items) : IVendorRepository
{
    private readonly List<Vendor> Items = [.. items];
    public Task<Vendor?> FindAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Items.SingleOrDefault(v => v.Id == id));
    public Task<IReadOnlyList<Vendor>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Vendor>>(Items.AsReadOnly());
}

public sealed class InMemoryVendorInvoiceRepository(IEnumerable<VendorInvoice> items) : IVendorInvoiceRepository
{
    private readonly List<VendorInvoice> Items = [.. items];
    public Task<VendorInvoice?> FindAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Items.SingleOrDefault(i => i.Id == id));
    public Task<IReadOnlyList<VendorInvoice>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<VendorInvoice>>(Items.AsReadOnly());
    public Task<bool> ExistsDuplicateAsync(Guid vendorId, string normalizedNumber, Guid? excludingInvoiceId, CancellationToken ct = default) =>
        Task.FromResult(Items.Any(i => i.VendorId == vendorId && i.NormalizedInvoiceNumber == normalizedNumber && i.Id != excludingInvoiceId));
    public Task AddAsync(VendorInvoice invoice, CancellationToken ct = default)
    {
        Items.Add(invoice);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryEvaluationRecordRepository(IEnumerable<EvaluationRecord> items) : IEvaluationRecordRepository
{
    private readonly List<EvaluationRecord> Items = [.. items];
    public Task AddAsync(EvaluationRecord record, CancellationToken ct = default)
    {
        Items.Add(record);
        return Task.CompletedTask;
    }
    public Task<EvaluationRecord?> FindAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Items.SingleOrDefault(r => r.Id == id));
    public Task<IReadOnlyList<EvaluationRecord>> ListByTransactionAsync(string transactionRef, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<EvaluationRecord>>(Items.Where(r => r.TransactionRef == transactionRef).ToList());
}

public sealed class InMemoryRuleDefinitionRepository(IEnumerable<RuleDefinition> items) : IRuleDefinitionRepository
{
    private readonly List<RuleDefinition> Items = [.. items];
    public Task<IReadOnlyList<RuleDefinition>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<RuleDefinition>>(Items.AsReadOnly());
}
