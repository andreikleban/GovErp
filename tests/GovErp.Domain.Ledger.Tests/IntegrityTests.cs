using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Ledger.Exceptions;

namespace GovErp.Domain.Ledger.Tests;

public sealed class IntegrityTests
{
    private static readonly AccountCode Account = AccountCode.Parse("701-6000-53100-G-COPS-26");

    [Fact]
    public void Posted_journal_does_not_expose_its_mutable_list()
    {
        var entry = JournalEntry.Create("INV", [new(Account, LedgerFamily.Financial, Money.Of(1), Money.Zero, "Expense"),
            new(Account.WithObject(new("2100")), LedgerFamily.Financial, Money.Zero, Money.Of(1), "AP")],
            new(2026, 6), UserId.New(), DateTimeOffset.UnixEpoch);
        Assert.False(entry.Lines is List<JournalLine>);
        var collection = Assert.IsAssignableFrom<ICollection<JournalLine>>(entry.Lines);
        Assert.Throws<NotSupportedException>(() => collection.Clear());
        Assert.Equal(2, entry.Lines.Count);
    }

    [Fact]
    public void Invalid_period_actor_and_default_fiscal_year_are_rejected()
    {
        Assert.Throws<LedgerException>(() => new FiscalPeriod(0, 6));
        Assert.Throws<LedgerException>(() => new BudgetLine(Account, default, BudgetControlMode.Hard, Money.Of(1)));
        Assert.Throws<LedgerException>(() => JournalEntry.Create("INV",
            [new(Account, LedgerFamily.Financial, Money.Of(1), Money.Zero, "expense"),
             new(Account, LedgerFamily.Financial, Money.Zero, Money.Of(1), "AP")],
            new(2026, 6), default, DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void Opening_date_and_amendments_must_belong_to_budget_year()
    {
        Assert.Throws<LedgerException>(() => new OpeningBalance(Account, new(2026), new(2026, 7, 1), Money.Zero, Money.Zero, "seed"));
        var budget = new BudgetLine(Account, new(2026), BudgetControlMode.Hard, Money.Of(10));
        Assert.Throws<LedgerException>(() => budget.Amend(Money.Of(1), "BA", new(2027, 1, 1)));
        Assert.Equal(0, budget.ChangeStamp);
    }

    [Fact]
    public void Journal_line_rejects_unknown_family_and_null_account()
    {
        Assert.Throws<ArgumentNullException>(() => new JournalLine(null!, LedgerFamily.Financial, Money.Of(1), Money.Zero, "bad"));
        Assert.Throws<LedgerException>(() => new JournalLine(Account, (LedgerFamily)999, Money.Of(1), Money.Zero, "bad"));
    }
}
