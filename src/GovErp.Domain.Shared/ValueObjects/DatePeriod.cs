namespace GovErp.Domain.Shared.ValueObjects;

/// <summary>
/// A closed date range: from is on or before to.
/// </summary>
public sealed record DatePeriod
{
    public DateOnly From { get; }
    public DateOnly? To { get; }
    public DatePeriod(DateOnly from, DateOnly? to)
    {
        if (to < from) throw new InvalidValueException(nameof(to), ValueErrors.PeriodOrder, ("from", from), ("to", to));
        From = from;
        To = to;
    }
    public bool Contains(DateOnly date) => date >= From && (To is null || date <= To);
}
