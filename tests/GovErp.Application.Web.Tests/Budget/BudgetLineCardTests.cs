using GovErp.Application.Web.Budget;
using GovErp.Application.Web.Budget.Commands;
using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Infrastructure.Seed;

namespace GovErp.Application.Web.Tests.Budget;

[Collection("sql")]
public sealed class BudgetLineCardTests(SqlServerFixture fixture)
{
    private const string General = "101-6000-53100";
    private const string Police = "701-3000-53100-G-COPS-26";
    private const string Po = "PO-2026-0451";

    [Fact]
    public async Task Submitted_non_po_invoice_appears_as_a_held_reservation_with_its_invoice_reference()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(10_000m, null, (General, 10_000m, null));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var reference = (await t.GetAsync(inv.Id)).Reference;

        var detail = await t.Service<IBudgetAppService>().GetLineAsync(General, 2026, t.BudgetOfficer);
        var reservation = detail.Reservations.Should().ContainSingle().Subject;
        reservation.InvoiceId.Should().Be(inv.Id);
        reservation.InvoiceReference.Should().Be(reference);
        reservation.Amount.Should().Be(10_000m);
        reservation.Status.Should().Be("Held");
        detail.Line.Account.Should().Be(General);
        detail.Encumbrances.Should().BeEmpty();
    }

    [Fact]
    public async Task Posted_po_backed_invoice_shows_committed_reservation_and_the_encumbrance_on_the_account()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(164_800m, Po, (Police, 164_800m, 1));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        await t.ApproveThroughAsync(inv.Id);
        (await t.PostAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var reference = (await t.GetAsync(inv.Id)).Reference;

        var detail = await t.Service<IBudgetAppService>().GetLineAsync(Police, 2026, t.BudgetOfficer);
        // 4,800 excess reserved directly on the account, now committed to actuals by Post.
        var reservation = detail.Reservations.Should().ContainSingle().Subject;
        reservation.InvoiceReference.Should().Be(reference);
        reservation.Status.Should().Be("Committed");

        var encumbrance = detail.Encumbrances.Should().ContainSingle(e => e.PoLineRef == $"{Po}/1").Subject;
        var claim = encumbrance.Claims.Should().ContainSingle().Subject;
        claim.InvoiceReference.Should().Be(reference);
        claim.Amount.Should().Be(160_000m);
        claim.Status.Should().Be("Consumed");
        var billing = encumbrance.BillingClaims.Should().ContainSingle().Subject;
        billing.InvoiceReference.Should().Be(reference);
        billing.Amount.Should().Be(164_800m);
    }

    [Fact]
    public async Task Unknown_account_or_fiscal_year_is_not_found()
    {
        var t = await fixture.CreateTenantAsync();
        var service = t.Service<IBudgetAppService>();
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetLineAsync(General, 2099, t.BudgetOfficer));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetLineAsync("999-0000-00000", 2026, t.BudgetOfficer));
    }

    [Fact]
    public async Task Amendments_journal_lists_amendments_of_every_account()
    {
        var t = await fixture.CreateTenantAsync();
        var command = new AmendBudgetCommand(TenantDriver.Env(), General, 2026, 5_000m, "BA-2026-01", SpringfieldData.Jun15);
        var result = await t.Service<IBudgetAppService>().AmendAsync(command, t.BudgetOfficer);
        result.IsAccepted.Should().BeTrue(result.Reason);

        var amendments = await t.Service<IBudgetAppService>().ListAmendmentsAsync(2026, t.BudgetOfficer);
        amendments.Should().Contain(a => a.Account == General && a.Reference == "BA-2026-01" && a.Amount == 5_000m);
    }
}
