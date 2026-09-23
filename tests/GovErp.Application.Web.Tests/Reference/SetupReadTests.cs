using GovErp.Application.Web.Approvals;
using GovErp.Application.Web.Budget;
using GovErp.Application.Web.Budget.Commands;
using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Invoices;
using GovErp.Application.Web.Invoices.Commands;
using GovErp.Application.Web.Posting;
using GovErp.Application.Web.Reference;
using GovErp.Infrastructure.Seed;

namespace GovErp.Application.Web.Tests.Reference;

[Collection("sql")]
public sealed class SetupReadTests(SqlServerFixture fixture)
{
    private const string General = "101-6000-53100";
    private const string Grant = "G-COPS-26";

    [Fact]
    public async Task Fund_and_grant_cards_include_combinations_and_budget_lines_unknown_codes_are_not_found()
    {
        var t = await fixture.CreateTenantAsync();
        var reference = t.Service<IReferenceAppService>();

        var fund = await reference.GetFundAsync("101", t.Clerk);
        fund.Name.Should().Be("General Fund");
        fund.Combinations.Should().Contain(c => c.Code == General);
        fund.BudgetLines.Should().Contain(l => l.Account == General && l.FiscalYear == 2026);

        var grant = await reference.GetGrantAsync(Grant, t.Clerk);
        grant.Name.Should().Contain("COPS");
        grant.Combinations.Should().NotBeEmpty();
        grant.BudgetLines.Should().NotBeEmpty();
        grant.Combinations.Should().OnlyContain(c => c.Code.Contains(Grant, StringComparison.Ordinal));

        await Assert.ThrowsAsync<NotFoundException>(() => reference.GetFundAsync("999", t.Clerk));
        await Assert.ThrowsAsync<NotFoundException>(() => reference.GetGrantAsync("G-MISSING", t.Clerk));
    }

    [Fact]
    public async Task Users_of_springfield_are_invisible_to_shelbyville()
    {
        var springfield = await fixture.Named("springfield").Service<IReferenceAppService>()
            .GetUsersAsync(fixture.Named("springfield").Clerk);
        springfield.Should().Contain(u => u.UserName == "ap.clerk" && u.DisplayName == "AP Clerk");
        springfield.Should().NotContain(u => u.UserName.StartsWith("shelby", StringComparison.Ordinal));

        var shelbyville = await fixture.Named("shelbyville").Service<IReferenceAppService>()
            .GetUsersAsync(fixture.Named("shelbyville").Clerk);
        shelbyville.Should().Contain(u => u.UserName == "shelby.clerk");
        shelbyville.Should().NotContain(u => u.UserName == "ap.clerk");
    }

    [Fact]
    public async Task Role_matrix_cells_match_what_the_command_services_accept_or_forbid()
    {
        var t = await fixture.CreateTenantAsync();
        var matrix = await t.Service<IReferenceAppService>().GetRoleMatrixAsync(t.Clerk);
        var clerk = matrix.Rows.Should().ContainSingle(r => r.Role == Roles.ApClerk).Subject;
        var departmentHead = matrix.Rows.Should().ContainSingle(r => r.Role == Roles.DepartmentHead).Subject;
        var budgetOfficer = matrix.Rows.Should().ContainSingle(r => r.Role == Roles.BudgetOfficer).Subject;

        clerk.CanCreateAndSubmit.Should().BeTrue();
        clerk.CanApprove.Should().BeFalse();
        departmentHead.CanPost.Should().BeFalse();
        budgetOfficer.CanAmendBudget.Should().BeTrue();
        budgetOfficer.CanPost.Should().BeTrue();
        matrix.ReleaseSoftStopRoles.Should().Contain(Roles.BudgetOfficer).And.Contain(Roles.FinanceDirector);
        matrix.SeparationOfDutiesNote.Should().Contain("author");

        var inv = await t.CreateAsync(10_000m, null, (General, 10_000m, null));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var clerkApprove = await t.Service<IApprovalAppService>().ApproveAsync(Action(await t.GetAsync(inv.Id)), t.Clerk);
        clerkApprove.Status.Should().Be(CommandStatus.Forbidden);

        const string fire = "701-6000-53100-G-COPS-26";
        var author = new ActorContext(t.Tenant, new UserId(Guid.NewGuid()), "author",
            new HashSet<string> { Roles.ApClerk, Roles.DepartmentHead }, "6000");
        var own = await t.Service<IInvoiceAppService>().CreateDraftAsync(new CreateInvoiceCommand(
            TenantDriver.Env(), $"T-{Guid.NewGuid():N}"[..12], SpringfieldData.AcmeId,
            SpringfieldData.Jun15, SpringfieldData.Jun15, SpringfieldData.Jun15, SpringfieldData.Jul15, 30_000m, null,
            [new DistributionCommand(fire, 30_000m, null)]), author);
        own.IsAccepted.Should().BeTrue(own.Reason);
        (await t.Service<IInvoiceAppService>().SubmitAsync(Action(own.Value!), author)).IsAccepted.Should().BeTrue();
        var ownApprove = await t.Service<IApprovalAppService>().ApproveAsync(Action(await t.GetAsync(own.Value!.Id)), author);
        ownApprove.Status.Should().Be(CommandStatus.Forbidden);

        var po = await t.CreateAsync(160_000m, "PO-2026-0451", ("701-3000-53100-G-COPS-26", 160_000m, 1));
        (await t.SubmitAsync(po.Id)).IsAccepted.Should().BeTrue();
        await t.ApproveThroughAsync(po.Id);
        var headPost = await t.Service<IPostingAppService>().PostAsync(Action(await t.GetAsync(po.Id)), t.FireChief);
        headPost.Status.Should().Be(CommandStatus.Forbidden);

        var amend = await t.Service<IBudgetAppService>().AmendAsync(
            new AmendBudgetCommand(TenantDriver.Env(), General, 2026, 1_000m, "BA-MATRIX", SpringfieldData.Jun15), t.BudgetOfficer);
        amend.IsAccepted.Should().BeTrue(amend.Reason);
    }

    private static InvoiceActionCommand Action(GovErp.Application.Web.Invoices.Contracts.InvoiceVm invoice) =>
        new(TenantDriver.Env(invoice.RowVersion), invoice.Id);
}
