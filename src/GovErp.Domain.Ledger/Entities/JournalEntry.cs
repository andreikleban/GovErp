using GovErp.Domain.Ledger.Exceptions;

namespace GovErp.Domain.Ledger.Entities;

/// <summary>
/// A posted journal entry. Immutable after creation. Balanced per fund and account family.
/// </summary>
public sealed class JournalEntry
{
    private readonly List<JournalLine> _lines = [];

    public Guid Id { get; private set; }
    public string SourceRef { get; private set; }
    public int PeriodYear { get; private set; }
    public int PeriodMonth { get; private set; }
    public UserId PostedBy { get; private set; }
    public DateTimeOffset PostedAt { get; private set; }
    public IReadOnlyList<JournalLine> Lines => _lines.AsReadOnly();

    private JournalEntry(string sourceRef, IEnumerable<JournalLine> lines, FiscalPeriod period, UserId postedBy, DateTimeOffset postedAt)
    {
        Id = Guid.NewGuid();
        SourceRef = sourceRef;
        PeriodYear = period.Year;
        PeriodMonth = period.Month;
        PostedBy = postedBy;
        PostedAt = postedAt;
        _lines.AddRange(lines);
    }

    private JournalEntry() { SourceRef = null!; }

    public static JournalEntry Create(string sourceRef, IReadOnlyList<JournalLine> lines, FiscalPeriod period,
        UserId postedBy, DateTimeOffset postedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRef);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(period);
        if (postedBy.Value == Guid.Empty || lines.Any(l => l is null))
            throw new LedgerException(LedgerErrors.JournalIncomplete);
        if (!period.IsOpen)
        {
            throw new LedgerException(LedgerErrors.PeriodClosed, ("period", $"{period.Year}-{period.Month:00}"));
        }

        if (lines.Count < 2)
        {
            throw new LedgerException(LedgerErrors.JournalTooShort);
        }

        foreach (var group in lines.GroupBy(l => (l.Account.Fund, l.Family)))
        {
            var debit = group.Aggregate(Money.Zero, (s, l) => s + l.Debit);
            var credit = group.Aggregate(Money.Zero, (s, l) => s + l.Credit);
            if (debit != credit)
            {
                throw new LedgerException(LedgerErrors.JournalUnbalanced, ("source", sourceRef), ("fund", group.Key.Fund),
                    ("family", group.Key.Family), ("debit", debit), ("credit", credit));
            }
        }

        return new JournalEntry(sourceRef, lines, period, postedBy, postedAt);
    }
}
