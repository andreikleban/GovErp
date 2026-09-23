using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Ledger.Repositories;
using GovErp.Domain.Payables.Entities;
using GovErp.Domain.Payables.Repositories;

namespace GovErp.Application.Web.Tests;

/// <summary>
/// Точка синхронизации гонки: первые Participants прибытий ждут друг друга (все прочитали данные), остальные проходят сразу —
/// так повтор после конфликта и повторные чтения в той же операции не зависают.
/// </summary>
public sealed class SyncPoint(int participants)
{
    private readonly Barrier _barrier = new(participants);
    private int _arrivals;

    public void Arrive()
    {
        if (Interlocked.Increment(ref _arrivals) <= participants)
        {
            _barrier.SignalAndWait(TimeSpan.FromSeconds(30));
        }
    }
}

public sealed class TestHooks
{
    public SyncPoint? AfterBudgetRead { get; set; }
    public SyncPoint? AfterEncumbranceRead { get; set; }
    public SyncPoint? AfterDuplicateCheck { get; set; }
    public string? FailOnSecondLookupOf { get; set; }
    public bool FailJournalWrite { get; set; }

    public void Reset() { AfterBudgetRead = AfterEncumbranceRead = AfterDuplicateCheck = null; FailOnSecondLookupOf = null; FailJournalWrite = false; }
}

public sealed class HookedBudgetLineRepository(IBudgetLineRepository inner, TestHooks hooks) : IBudgetLineRepository
{
    private readonly Dictionary<AccountCode, int> _lookups = [];

    public async Task<BudgetLine?> FindAsync(AccountCode account, FiscalYear fy, CancellationToken ct = default)
    {
        var line = await inner.FindAsync(account, fy, ct);
        _lookups[account] = _lookups.GetValueOrDefault(account) + 1;
        if (hooks.FailOnSecondLookupOf == account.ToString() && _lookups[account] == 2)
        {
            throw new InvalidOperationException("Injected failure on budget line lookup.");
        }

        hooks.AfterBudgetRead?.Arrive();
        return line;
    }

    public Task<IReadOnlyList<BudgetLine>> ListAsync(FiscalYear fy, CancellationToken ct = default) => inner.ListAsync(fy, ct);
    public Task<BudgetLine?> FindByReservationAsync(Guid reservationId, CancellationToken ct = default) => inner.FindByReservationAsync(reservationId, ct);
    public Task AddAsync(BudgetLine line, CancellationToken ct = default) => inner.AddAsync(line, ct);
}

/// <summary>Сборщик снимка читает строку PO через encumbrance: барьер гонки claims стоит после FindByPoLineAsync.</summary>
public sealed class HookedEncumbranceRepository(IEncumbranceRepository inner, TestHooks hooks) : IEncumbranceRepository
{
    public async Task<Encumbrance?> FindByPoLineAsync(string poLineRef, CancellationToken ct = default)
    {
        var encumbrance = await inner.FindByPoLineAsync(poLineRef, ct);
        hooks.AfterEncumbranceRead?.Arrive();
        return encumbrance;
    }

    public Task<Encumbrance?> FindByClaimAsync(Guid claimId, CancellationToken ct = default) => inner.FindByClaimAsync(claimId, ct);
    public Task<IReadOnlyList<Encumbrance>> ListAsync(CancellationToken ct = default) => inner.ListAsync(ct);
    public Task AddAsync(Encumbrance encumbrance, CancellationToken ct = default) => inner.AddAsync(encumbrance, ct);
}

public sealed class HookedVendorInvoiceRepository(IVendorInvoiceRepository inner, TestHooks hooks) : IVendorInvoiceRepository
{
    public async Task<bool> ExistsDuplicateAsync(Guid vendorId, string normalizedNumber, Guid? excludingInvoiceId, CancellationToken ct = default)
    {
        var exists = await inner.ExistsDuplicateAsync(vendorId, normalizedNumber, excludingInvoiceId, ct);
        hooks.AfterDuplicateCheck?.Arrive();
        return exists;
    }

    public Task<VendorInvoice?> FindAsync(Guid id, CancellationToken ct = default) => inner.FindAsync(id, ct);
    public Task<VendorInvoice?> FindByReferenceAsync(string reference, CancellationToken ct = default) => inner.FindByReferenceAsync(reference, ct);
    public Task<IReadOnlyList<VendorInvoice>> ListAsync(CancellationToken ct = default) => inner.ListAsync(ct);
    public Task AddAsync(VendorInvoice invoice, CancellationToken ct = default) => inner.AddAsync(invoice, ct);
}

public sealed class HookedJournalRepository(IJournalRepository inner, TestHooks hooks) : IJournalRepository
{
    public Task AddAsync(JournalEntry entry, CancellationToken ct = default) =>
        hooks.FailJournalWrite ? throw new InvalidOperationException("Injected failure on journal write.") : inner.AddAsync(entry, ct);

    public Task<IReadOnlyList<JournalEntry>> ListBySourceAsync(string sourceRef, CancellationToken ct = default) => inner.ListBySourceAsync(sourceRef, ct);
    public Task<IReadOnlyList<JournalEntry>> ListAsync(CancellationToken ct = default) => inner.ListAsync(ct);
}
