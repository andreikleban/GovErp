using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Ledger.Exceptions;

namespace GovErp.Domain.Ledger.Tests;

public class JournalEntryTests
{
    private static readonly UserId Poster = UserId.New();
    private static readonly DateTimeOffset At = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly FiscalPeriod OpenSep = new(2026, 9);

    private static JournalLine Dr(string account, decimal amount, LedgerFamily family = LedgerFamily.Financial) =>
        new(AccountCode.Parse(account), family, Money.Of(amount), Money.Zero, "test");

    private static JournalLine Cr(string account, decimal amount, LedgerFamily family = LedgerFamily.Financial) =>
        new(AccountCode.Parse(account), family, Money.Zero, Money.Of(amount), "test");

    [Fact]
    public void Balanced_single_fund_entry_is_created()
    {
        var entry = JournalEntry.Create("INV-1",
            [Dr("701-6000-53100-G-COPS-26", 160_000m), Cr("701-0000-2100", 160_000m)], OpenSep, Poster, At);
        entry.Lines.Should().HaveCount(2);
        entry.SourceRef.Should().Be("INV-1");
    }

    [Fact]
    public void Multi_fund_entry_must_balance_per_fund()
    {
        // The total balances (30k/30k), but fund 101 is Dr 12k / Cr 8k, fund 202 is Dr 8k / Cr 12k.
        FluentActions.Invoking(() => JournalEntry.Create("INV-2",
            [Dr("101-6000-53100", 12_000m), Cr("101-0000-2100", 8_000m),
             Dr("202-4000-53100", 8_000m),  Cr("202-0000-2100", 12_000m),
             Dr("501-5000-53100", 10_000m), Cr("501-0000-2100", 10_000m)], OpenSep, Poster, At))
            .Should().Throw<LedgerException>().WithMessage("*101*");
    }

    [Fact]
    public void Multi_fund_balanced_entry_is_created()
    {
        var entry = JournalEntry.Create("INV-2",
            [Dr("101-6000-53100", 12_000m), Cr("101-0000-2100", 12_000m),
             Dr("202-4000-53100", 8_000m),  Cr("202-0000-2100", 8_000m),
             Dr("501-5000-53100", 10_000m), Cr("501-0000-2100", 10_000m)], OpenSep, Poster, At);
        entry.Lines.Should().HaveCount(6);
    }

    [Fact]
    public void Budgetary_and_financial_families_balance_separately()
    {
        // Encumbrance reversal (Budgetary) + expense (Financial): both pairs within their own families.
        var entry = JournalEntry.Create("INV-3",
            [Dr("701-0000-2900-G-COPS-26", 160_000m, LedgerFamily.Budgetary), Cr("701-3000-5900-G-COPS-26", 160_000m, LedgerFamily.Budgetary),
             Dr("701-3000-53100-G-COPS-26", 160_000m), Cr("701-0000-2100", 160_000m)], OpenSep, Poster, At);
        entry.Lines.Should().HaveCount(4);

        FluentActions.Invoking(() => JournalEntry.Create("INV-4",
            [Dr("701-0000-2900-G-COPS-26", 160_000m, LedgerFamily.Budgetary), Cr("701-0000-2100", 160_000m)], OpenSep, Poster, At))
            .Should().Throw<LedgerException>();
    }

    [Fact]
    public void Closed_period_is_rejected()
    {
        var closed = new FiscalPeriod(2026, 8);
        closed.Close();
        FluentActions.Invoking(() => JournalEntry.Create("INV-1",
            [Dr("701-6000-53100-G-COPS-26", 1m), Cr("701-0000-2100", 1m)], closed, Poster, At))
            .Should().Throw<LedgerException>();
    }

    [Fact]
    public void Fewer_than_two_lines_is_rejected() =>
        FluentActions.Invoking(() => JournalEntry.Create("INV-1", [Dr("701-6000-53100-G-COPS-26", 1m)], OpenSep, Poster, At))
            .Should().Throw<LedgerException>();

    [Fact]
    public void Line_with_both_debit_and_credit_is_rejected() =>
        FluentActions.Invoking(() => new JournalLine(AccountCode.Parse("101-6000-53100"), LedgerFamily.Financial,
                Money.Of(1), Money.Of(1), "bad"))
            .Should().Throw<LedgerException>();

    [Fact]
    public void FiscalPeriod_key_from_date() =>
        FiscalPeriod.KeyFor(new DateOnly(2026, 9, 21)).Should().Be((2026, 9));
}
