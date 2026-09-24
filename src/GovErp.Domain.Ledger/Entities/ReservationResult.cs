namespace GovErp.Domain.Ledger.Entities;

/// <summary>
/// Result of a reservation: whether available budget covered it, and the shortfall.
/// </summary>
public sealed record ReservationResult(bool IsReserved, Guid? ReservationId, Money AvailableBefore, Money Shortfall, bool IsOverage)
{
    public static ReservationResult Reserved(Guid id, Money availableBefore, bool overage) =>
        new(true, id, availableBefore, Money.Zero, overage);

    public static ReservationResult Refused(Money availableBefore, Money shortfall) =>
        new(false, null, availableBefore, shortfall, true);
}
