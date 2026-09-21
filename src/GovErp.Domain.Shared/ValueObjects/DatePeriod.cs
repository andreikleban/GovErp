namespace GovErp.Domain.Shared.ValueObjects;

public sealed record DatePeriod
{
    public DateOnly From { get; }
    public DateOnly? To { get; }
    public DatePeriod(DateOnly from, DateOnly? to)
    {
        if (to < from) throw new ArgumentException("Period end precedes start.", nameof(to));
        From = from;
        To = to;
    }
    public bool Contains(DateOnly date) => date >= From && (To is null || date <= To);
}
