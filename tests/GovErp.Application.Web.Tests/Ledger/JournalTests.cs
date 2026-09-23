using GovErp.Application.Web.Ledger;
using GovErp.Application.Web.Ledger.Contracts;

namespace GovErp.Application.Web.Tests.Ledger;

[Collection("sql")]
public sealed class JournalTests(SqlServerFixture fixture)
{
    private const string General = "101-6000-53100";

    [Fact]
    public async Task Posting_creates_a_journal_entry_balanced_per_fund_and_family_and_linked_to_the_invoice()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(10_000m, null, (General, 10_000m, null));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        await t.ApproveThroughAsync(inv.Id);
        (await t.PostAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var reference = (await t.GetAsync(inv.Id)).Reference;

        var ledger = t.Service<ILedgerAppService>();
        var byFund = await ledger.ListJournalAsync(new JournalFilter(Fund: "101"), t.BudgetOfficer);
        var entry = byFund.Should().ContainSingle(e => e.SourceRef == reference).Subject;
        entry.InvoiceId.Should().Be(inv.Id);
        entry.InvoiceReference.Should().Be(reference);
        entry.Lines.Should().NotBeEmpty();
        entry.Balances.Should().NotBeEmpty();
        entry.Balances.Should().OnlyContain(b => b.Balanced);
        entry.Lines.Sum(l => l.Debit).Should().Be(entry.Lines.Sum(l => l.Credit));

        (await ledger.ListJournalAsync(new JournalFilter(Fund: "202"), t.BudgetOfficer)).Should().NotContain(e => e.SourceRef == reference);
        (await ledger.ListJournalAsync(new JournalFilter(Source: reference), t.BudgetOfficer)).Should().ContainSingle();
        (await ledger.ListJournalAsync(new JournalFilter(PeriodYear: 2026, PeriodMonth: 6), t.BudgetOfficer)).Should().Contain(e => e.SourceRef == reference);
        (await ledger.ListJournalAsync(new JournalFilter(PeriodYear: 2026, PeriodMonth: 7), t.BudgetOfficer)).Should().NotContain(e => e.SourceRef == reference);
    }

    [Fact]
    public async Task Periods_list_shows_only_the_demo_business_month_open()
    {
        var t = await fixture.CreateTenantAsync();
        var periods = await t.Service<ILedgerAppService>().ListPeriodsAsync(t.BudgetOfficer);
        periods.Should().HaveCount(12);
        periods.Should().ContainSingle(p => p.Status == "Open").Which.Should().BeEquivalentTo(new FiscalPeriodVm(2026, 6, "Open"));
    }

    [Fact]
    public async Task A_second_tenant_does_not_see_the_first_tenants_journal()
    {
        var t1 = await fixture.CreateTenantAsync();
        var inv = await t1.CreateAsync(10_000m, null, (General, 10_000m, null));
        (await t1.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        await t1.ApproveThroughAsync(inv.Id);
        (await t1.PostAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var reference = (await t1.GetAsync(inv.Id)).Reference;

        var t2 = await fixture.CreateTenantAsync();
        var journal = await t2.Service<ILedgerAppService>().ListJournalAsync(JournalFilter.None, t2.BudgetOfficer);
        journal.Should().NotContain(e => e.SourceRef == reference);
    }
}
