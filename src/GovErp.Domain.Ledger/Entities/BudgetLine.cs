using GovErp.Domain.Ledger.Exceptions;
namespace GovErp.Domain.Ledger.Entities;

/// <summary>
/// Budget of one account and fiscal year: adopted, actuals, encumbered and reservations.
/// </summary>
public sealed class BudgetLine
{
    private readonly List<BudgetAmendment> _amendments = [];
    private readonly List<BudgetReservation> _reservations = [];
    public Guid Id { get; private set; } = Guid.NewGuid();
    public AccountCode Account { get; private set; }
    public FiscalYear FiscalYear { get; private set; }
    public BudgetControlMode ControlMode { get; private set; }
    public Money Adopted { get; private set; }
    public Money Actuals { get; private set; }
    public Money Encumbered { get; private set; }
    public long ChangeStamp { get; private set; }
    public Guid? OpeningBalanceId { get; private set; }
    public IReadOnlyList<BudgetAmendment> Amendments => _amendments.AsReadOnly();
    public IReadOnlyList<BudgetReservation> Reservations => _reservations.AsReadOnly();
    public Money Amended => _amendments.Aggregate(Adopted, (s, a) => s + a.Amount);
    public Money Held => _reservations.Where(r => r.Status == ReservationStatus.Held).Aggregate(Money.Zero, (s, r) => s + r.Amount);
    public Money Available => Amended - Actuals - Encumbered - Held;

    private BudgetLine() { Account = null!; }

    public BudgetLine(AccountCode account, FiscalYear fiscalYear, BudgetControlMode controlMode, Money adopted)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (adopted.IsNegative || !Enum.IsDefined(controlMode) || fiscalYear.Year is < 2 or > 9999) throw new LedgerException(LedgerErrors.InvalidBudgetConfiguration);
        Account = account; FiscalYear = fiscalYear; ControlMode = controlMode; Adopted = adopted;
    }

    public Money OwnHeld(Guid invoiceId, int contentVersion) => _reservations
        .Where(r => r.InvoiceId == invoiceId && r.ContentVersion == contentVersion && r.Status == ReservationStatus.Held)
        .Aggregate(Money.Zero, (s, r) => s + r.Amount);
    public Money AvailableForInvoice(Guid invoiceId, int contentVersion) => Available + OwnHeld(invoiceId, contentVersion);

    public void Amend(Money amount, string reference, DateOnly effectiveDate)
    {
        if (amount.IsZero) throw new LedgerException(LedgerErrors.ZeroAmendment);
        if (!FiscalYear.Contains(effectiveDate)) throw new LedgerException(LedgerErrors.AmendmentOutsideYear, ("date", effectiveDate), ("fiscalYear", FiscalYear.Year));
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        _ = Amended + amount;
        _amendments.Add(new(amount, reference, effectiveDate)); ChangeStamp++;
    }

    public ReservationResult Reserve(Guid invoiceId, int contentVersion, Money amount, string sourceRef)
    {
        LedgerGuard.Owner(invoiceId, contentVersion); LedgerGuard.Positive(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRef);
        var available = Available; var overage = amount > available;
        if (overage && ControlMode == BudgetControlMode.Hard) return ReservationResult.Refused(available, amount - available);
        _ = Held + amount;
        _reservations.Add(new(Guid.NewGuid(), invoiceId, contentVersion, sourceRef, amount)); ChangeStamp++;
        return new(true, _reservations[^1].Id, available, overage ? amount - available : Money.Zero, overage);
    }

    public void Commit(Guid reservationId) => CommitHeld(FindHeld(reservationId));
    public void Commit(Guid reservationId, Guid invoiceId, int contentVersion) => CommitHeld(FindHeld(reservationId, invoiceId, contentVersion));
    private void CommitHeld(BudgetReservation reservation)
    {
        var actuals = Actuals + reservation.Amount;
        reservation.Commit(); Actuals = actuals; ChangeStamp++;
    }
    public void Release(Guid reservationId) { FindHeld(reservationId).Release(); ChangeStamp++; }
    public void Release(Guid reservationId, Guid invoiceId, int contentVersion) { FindHeld(reservationId, invoiceId, contentVersion).Release(); ChangeStamp++; }

    public void ApplyOpeningBalance(OpeningBalance opening)
    {
        ArgumentNullException.ThrowIfNull(opening);
        if (ChangeStamp != 0 || OpeningBalanceId.HasValue || opening.Account != Account || opening.FiscalYear != FiscalYear)
            throw new LedgerException(LedgerErrors.OpeningBalanceNeedsPristineLine);
        Actuals = opening.InitialActuals; Encumbered = opening.InitialEncumbered; OpeningBalanceId = opening.Id; ChangeStamp++;
    }
    public void RecordLiquidation(Money amount)
    {
        LedgerGuard.Positive(amount);
        if (amount > Encumbered) throw new LedgerException(LedgerErrors.LiquidationExceedsEncumbered, ("amount", amount), ("encumbered", Encumbered));
        var actuals = Actuals + amount;
        Encumbered -= amount; Actuals = actuals; ChangeStamp++;
    }
    private BudgetReservation FindHeld(Guid id, Guid? invoiceId = null, int? version = null)
    {
        var r = _reservations.SingleOrDefault(r => r.Id == id) ?? throw new LedgerException(LedgerErrors.UnknownReservation);
        if (r.Status != ReservationStatus.Held || (invoiceId.HasValue && (r.InvoiceId != invoiceId || r.ContentVersion != version)))
            throw new LedgerException(LedgerErrors.ReservationNotHeld);
        return r;
    }
}
