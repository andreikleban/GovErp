using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Invoices;
using GovErp.Application.Web.Invoices.Commands;
using GovErp.Application.Web.Invoices.Contracts;

namespace GovErp.Application.Web.Tests.Invoices;

[Collection("sql")]
public sealed class InvoiceListAndFundsTests(SqlServerFixture fixture)
{
    private const string General = "101-6000-53100";
    private const string Streets = "202-4000-53100";
    private const string Fire = "701-6000-53100-G-COPS-26";
    private const string Police = "701-3000-53100-G-COPS-26";
    private const string Po = "PO-2026-0451";

    [Fact]
    public async Task Non_po_invoice_reserves_its_total_and_consumes_it_on_post()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(10_000m, null, (General, 10_000m, null));
        (await t.GetAsync(inv.Id)).Funds.Should().Be(InvoiceFundsVm.Empty);

        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var submitted = await t.GetAsync(inv.Id);
        submitted.Funds.Should().Be(new InvoiceFundsVm(10_000m, 0m, 0m, 0m));

        await t.ApproveThroughAsync(inv.Id);
        (await t.PostAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var posted = await t.GetAsync(inv.Id);
        posted.Funds.Should().Be(new InvoiceFundsVm(0m, 0m, 0m, 10_000m));
        posted.PostedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Po_backed_invoice_shows_liquidation_and_billing_claims_and_reserves_only_the_excess()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(164_800m, Po, (Police, 164_800m, 1));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();

        var submitted = await t.GetAsync(inv.Id);
        submitted.Funds.Should().Be(new InvoiceFundsVm(4_800m, 160_000m, 164_800m, 0m));

        (await t.Service<IInvoiceAppService>().WithdrawAsync(
            new ReasonedActionCommand(TenantDriver.Env(submitted.RowVersion), inv.Id, "wrong amount"), t.Clerk)).IsAccepted.Should().BeTrue();
        (await t.GetAsync(inv.Id)).Funds.Should().Be(InvoiceFundsVm.Empty);
    }

    [Fact]
    public async Task Fully_liquidated_po_invoice_holds_no_reservation()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(160_000m, Po, (Police, 160_000m, 1));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        (await t.GetAsync(inv.Id)).Funds.Should().Be(new InvoiceFundsVm(0m, 160_000m, 160_000m, 0m));
    }

    [Fact]
    public async Task Available_after_matches_the_budget_line_after_submit()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(10_000m, null, (General, 6_000m, null), (General, 4_000m, null));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();

        var lines = (await t.GetAsync(inv.Id)).LastEvaluation!.LineBudgets;
        var available = (await t.BudgetAsync(General)).Available;
        lines.Select(l => (l.LineNo, l.Account, l.FiscalYear, l.AvailableAfter))
            .Should().Equal((1, General, 2026, available), (2, General, 2026, available));
    }

    [Fact]
    public async Task List_filters_by_status_fund_and_open_holds()
    {
        var t = await fixture.CreateTenantAsync();
        var service = t.Service<IInvoiceAppService>();
        var draft = await t.CreateAsync(1_000m, null, (General, 1_000m, null));
        var submitted = await t.CreateAsync(2_000m, null, (Streets, 2_000m, null));
        (await t.SubmitAsync(submitted.Id)).IsAccepted.Should().BeTrue();
        var blocked = await t.CreateAsync(160_000m, null, (Fire, 160_000m, null));
        var check = await service.ValidateAsync(new InvoiceActionCommand(TenantDriver.Env((await t.GetAsync(blocked.Id)).RowVersion), blocked.Id), t.Clerk);
        check.IsAccepted.Should().BeTrue(check.Reason);
        check.Value!.LastEvaluation!.Overall.Should().Be("HardStop");

        async Task<IReadOnlyList<Guid>> Ids(InvoiceListFilter filter) => (await service.ListAsync(filter, t.Clerk)).Select(i => i.Id).ToList();

        (await Ids(InvoiceListFilter.None)).Should().BeEquivalentTo([draft.Id, submitted.Id, blocked.Id]);
        (await Ids(new InvoiceListFilter(Status: "submitted"))).Should().Equal(submitted.Id);
        (await Ids(new InvoiceListFilter(Status: "Draft"))).Should().BeEquivalentTo([draft.Id, blocked.Id]);
        (await Ids(new InvoiceListFilter(Fund: "701"))).Should().Equal(blocked.Id);
        (await Ids(new InvoiceListFilter(Status: "Draft", Fund: "101"))).Should().Equal(draft.Id);
        (await Ids(new InvoiceListFilter(HasHolds: true))).Should().Equal(blocked.Id);
        (await Ids(new InvoiceListFilter(HasHolds: false))).Should().BeEquivalentTo([draft.Id, submitted.Id]);
        (await Ids(new InvoiceListFilter(Status: "Posted"))).Should().BeEmpty();

        var row = (await service.ListAsync(new InvoiceListFilter(Fund: "701"), t.Clerk)).Single();
        row.Funds.Should().Equal("701");
        row.OpenHolds.Should().BePositive();
    }
}
