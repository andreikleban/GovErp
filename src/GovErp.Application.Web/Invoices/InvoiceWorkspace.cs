using GovErp.Application.Web.Audit;
using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Invoices.Contracts;
using GovErp.Application.Web.Invoices.Mapping;
using GovErp.Application.Web.Validation;
using GovErp.Domain.Ledger.Exceptions;
using GovErp.Domain.Ledger.Repositories;
using GovErp.Domain.Payables.Entities;
using GovErp.Domain.Payables.Repositories;
using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.Repositories;
using GovErp.Domain.Validation.ValueObjects;
using PayablesRequirement = GovErp.Domain.Payables.Entities.ApprovalRequirement;

namespace GovErp.Application.Web.Invoices;

/// <summary>Общие шаги сценариев инвойса. Scoped: живёт в scope операции runner'а, все репозитории — на одном DbContext.</summary>
public sealed class InvoiceWorkspace(
    IVendorInvoiceRepository invoices, IVendorRepository vendors, IPurchaseOrderRepository purchaseOrders,
    IBudgetLineRepository budgetLines, IEncumbranceRepository encumbrances, IFiscalPeriodRepository periods,
    IJournalRepository journal, IEvaluationRecordRepository evaluations, IRuleDefinitionRepository rules,
    ValidationSubjectAssembler assembler, IAuditTrail audit, IConcurrencyGuard concurrency, IClock clock)
{
    private static readonly ValidationPipeline Pipeline = new(RuleCatalog.Default);
    private readonly Dictionary<Guid, EvaluationRecord> _evaluated = [];
    private IReadOnlyList<RuleDefinition>? _definitions;

    public IVendorInvoiceRepository Invoices => invoices;
    public IVendorRepository Vendors => vendors;
    public IPurchaseOrderRepository PurchaseOrders => purchaseOrders;
    public IBudgetLineRepository BudgetLines => budgetLines;
    public IEncumbranceRepository Encumbrances => encumbrances;
    public IFiscalPeriodRepository Periods => periods;
    public IJournalRepository Journal => journal;
    public IEvaluationRecordRepository Evaluations => evaluations;
    public IAuditTrail Audit => audit;
    public IConcurrencyGuard Concurrency => concurrency;
    public IClock Clock => clock;

    public async Task<VendorInvoice> LoadAsync(Guid id, CancellationToken ct) =>
        await invoices.FindAsync(id, ct) ?? throw new NotFoundException($"Invoice {id} not found.");

    /// <summary>Определения читаются один раз на операцию: вся команда оценивает по одному набору правил.</summary>
    public async Task<IReadOnlyList<RuleDefinition>> DefinitionsAsync(CancellationToken ct) => _definitions ??= await rules.ListAsync(ct);

    /// <summary>
    /// Оценка с сохранением записи. Для Submitted/Approved — Reevaluate: инвойс запоминает оценку, а смена fingerprint
    /// или базового маршрута открывает новый цикл; тогда оценка повторяется, чтобы последняя запись отражала новый цикл.
    /// </summary>
    public async Task<(EvaluationRecord Record, bool CycleRestarted)> EvaluateAsync(
        VendorInvoice invoice, EvaluationTrigger trigger, ActorContext actor, CancellationToken ct)
    {
        var record = await EvaluateOnceAsync(invoice, trigger, actor, ct);
        if (invoice.Status is not (InvoiceStatus.Submitted or InvoiceStatus.Approved))
        {
            return (record, false);
        }

        var cycle = invoice.ApprovalCycleId;
        invoice.Reevaluate(record.Id, record.TransactionVersion, record.RuleFingerprint, await RouteForAsync(record, ct));
        if (invoice.ApprovalCycleId == cycle)
        {
            return (record, false);
        }

        record = await EvaluateOnceAsync(invoice, trigger, actor, ct);
        invoice.Reevaluate(record.Id, record.TransactionVersion, record.RuleFingerprint, await RouteForAsync(record, ct));
        return (record, true);
    }

    /// <summary>Базовый маршрут (без требований override, D-6) — то, что хранит и проверяет VendorInvoice.</summary>
    public async Task<IReadOnlyList<PayablesRequirement>> RouteForAsync(EvaluationRecord record, CancellationToken ct)
    {
        var subject = record.InputSnapshot;
        var effective = RuleResolution.ResolveForSubject(await DefinitionsAsync(ct), subject);
        return ApprovalRouteResolver.Build(subject, [], effective)
            .Select(r => new PayablesRequirement(RoleMapping.ToPayables(r.Role), r.Department is null ? null : new DepartmentCode(r.Department)))
            .ToList();
    }

    /// <summary>
    /// Submit: резервы на бюджетных ключах, claims ликвидации и billing claims по строкам PO — из той же аллокации,
    /// что видел конвейер. Порядок ключей стабильный (меньше deadlock'ов). Проигранная гонка — LedgerException → Refused.
    /// </summary>
    public async Task<InvoiceHolds> HoldAsync(VendorInvoice invoice, ValidationSubject subject, CancellationToken ct)
    {
        var effective = RuleResolution.ResolveForSubject(await DefinitionsAsync(ct), subject);
        var allocation = BudgetAllocation.Allocate(subject);
        var fy = FiscalYear.FromDate(invoice.PostingDate);
        var cv = invoice.ContentVersion;
        static Money Sum(IEnumerable<Money> values) => values.Aggregate(Money.Zero, (s, m) => s + m);

        var reservations = new List<Guid>();
        foreach (var key in allocation.GroupBy(l => l.Distribution.Account).OrderBy(g => g.Key.ToString(), StringComparer.Ordinal))
        {
            var amount = Sum(key.Select(l => l.RequiredNewBudget));
            if (amount.IsZero)
            {
                continue;
            }

            var line = await budgetLines.FindAsync(key.Key, fy, ct) ?? throw new LedgerException($"Budget line {key.Key} {fy} not found.");
            var result = line.Reserve(invoice.Id, cv, amount, invoice.Reference);
            if (!result.IsReserved)
            {
                throw new LedgerException($"Budget on {key.Key} is no longer available: short by {result.Shortfall}.");
            }

            reservations.Add(result.ReservationId!.Value);
        }

        var claims = new List<Guid>();
        var billing = new List<Guid>();
        foreach (var po in allocation.Where(l => l.Distribution.Encumbrance is not null)
                     .GroupBy(l => l.Distribution.Encumbrance!.PoLineRef).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var encumbrance = await encumbrances.FindByPoLineAsync(po.Key, ct) ?? throw new LedgerException($"Encumbrance {po.Key} not found.");
            var liquidation = Sum(po.Select(l => l.LiquidationAmount));
            if (!liquidation.IsZero)
            {
                claims.Add(encumbrance.Claim(invoice.Id, cv, liquidation));
            }

            var account = po.First().Distribution.Account;
            var tolerance = effective.Find("PO_LIQUIDATION", account.Fund.Value, account.Grant?.Value)?.DecimalParameter("tolerance_pct") ?? 0m;
            billing.Add(encumbrance.ClaimBilling(invoice.Id, cv, Sum(po.Select(l => l.Distribution.Amount)), tolerance));
        }

        return new InvoiceHolds(reservations, claims, billing);
    }

    /// <summary>Reject / Withdraw: освобождает ровно то, что инвойс удерживал в своей версии содержания (GE-14).</summary>
    public async Task ReleaseAsync(InvoiceRelease release, CancellationToken ct)
    {
        foreach (var id in release.ReservationRefs)
        {
            (await budgetLines.FindByReservationAsync(id, ct) ?? throw new LedgerException($"Reservation {id} not found."))
                .Release(id, release.InvoiceId, release.ContentVersion);
        }

        foreach (var id in release.EncumbranceClaimRefs)
        {
            (await encumbrances.FindByClaimAsync(id, ct) ?? throw new LedgerException($"Claim {id} not found."))
                .ReleaseClaim(id, release.InvoiceId, release.ContentVersion);
        }

        foreach (var id in release.PoBillingClaimRefs)
        {
            (await encumbrances.FindByClaimAsync(id, ct) ?? throw new LedgerException($"Billing claim {id} not found."))
                .ReleaseBillingClaim(id, release.InvoiceId, release.ContentVersion);
        }
    }

    /// <summary>
    /// Оценка этой операции (ещё не сохранена — запрос к БД её не увидит), иначе LastEvaluationRef, иначе для Draft —
    /// последняя сохранённая проверка.
    /// </summary>
    public async Task<EvaluationRecord?> LastEvaluationAsync(VendorInvoice invoice, CancellationToken ct) =>
        _evaluated.GetValueOrDefault(invoice.Id)
        ?? (invoice.LastEvaluationRef is { } id
            ? await evaluations.FindAsync(id, ct)
            : (await evaluations.ListByTransactionAsync(invoice.Reference, ct)).LastOrDefault());

    public async Task<InvoiceVm> ToVmAsync(VendorInvoice invoice, CancellationToken ct, bool withRowVersion = false)
    {
        var vendor = await vendors.FindAsync(invoice.VendorId, ct) ?? throw new NotFoundException($"Vendor {invoice.VendorId} not found.");
        return InvoiceMapping.ToVm(invoice, vendor, await LastEvaluationAsync(invoice, ct),
            withRowVersion ? concurrency.VersionOf(invoice) : null, clock.BusinessDate);
    }

    public static string ReasonOf(EvaluationRecord record) =>
        string.Join(" ", record.Outcomes.Where(o => o.Severity >= Severity.SoftStop && !o.IsOverridden).Select(o => o.Message));

    private async Task<EvaluationRecord> EvaluateOnceAsync(VendorInvoice invoice, EvaluationTrigger trigger, ActorContext actor, CancellationToken ct)
    {
        var subject = await assembler.BuildAsync(invoice, clock.BusinessDate, ct);
        var record = Pipeline.Evaluate(subject, await DefinitionsAsync(ct), trigger, actor.UserId, clock.Now);
        await evaluations.AddAsync(record, ct);
        _evaluated[invoice.Id] = record;
        return record;
    }
}

public sealed record InvoiceHolds(IReadOnlyList<Guid> Reservations, IReadOnlyList<Guid> Claims, IReadOnlyList<Guid> BillingClaims);
