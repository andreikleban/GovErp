using GovErp.Application.Web.Approvals;
using GovErp.Application.Web.Budget;
using GovErp.Application.Web.Budget.Commands;
using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Explanation;
using GovErp.Application.Web.Invoices;
using GovErp.Application.Web.Invoices.Commands;
using GovErp.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Application.Web.Tests.Integration;

[Collection("sql")]
public sealed class LifecycleTests(SqlServerFixture fixture)
{
    private const string Fire = "701-6000-53100-G-COPS-26";
    private const string Police = "701-3000-53100-G-COPS-26";

    [Fact]
    public async Task ExerciseEndToEnd_HardStop_Amend_Override_Warning_Post()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(160_000m, null, (Fire, 160_000m, null));
        var refused = await t.SubmitAsync(inv.Id);
        refused.Status.Should().Be(CommandStatus.Refused);
        refused.Value!.LastEvaluation!.Overall.Should().Be("HardStop");

        (await t.Service<IBudgetAppService>().AmendAsync(new AmendBudgetCommand(TenantDriver.Env(), Fire, 2026, 13_000m, "BA-2026-14", SpringfieldData.Jun15), t.BudgetOfficer))
            .IsAccepted.Should().BeTrue();
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();   // a Soft Stop (procurement) does not block Submit
        await t.ApproveThroughAsync(inv.Id);                          // override → Warning (remaining 0 < 10%)
        (await t.GetAsync(inv.Id)).LastEvaluation!.Overall.Should().Be("Warning");
        (await t.PostAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var line = await t.BudgetAsync(Fire);
        (line.Actuals, line.Held, line.Available).Should().Be((292_000m, 0m, 0m));
    }

    [Fact]
    public async Task OpeningBalancesReconcile()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(160_000m, "PO-2026-0451", (Police, 160_000m, 1));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        await t.ApproveThroughAsync(inv.Id);
        (await t.PostAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var line = await t.BudgetAsync(Police);
        line.OpeningActuals.Should().Be(100_000m);                    // the snapshot does not change
        line.Actuals.Should().Be(line.OpeningActuals + 160_000m);
    }

    [Fact]
    public async Task ReapprovalAfterRuleChange()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(160_000m, "PO-2026-0451", (Police, 160_000m, 1));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        await t.ApproveThroughAsync(inv.Id);
        await t.WithDbAsync(async db =>
        {
            db.RuleDefinitions.Add(new Domain.Validation.Entities.RuleDefinition("BUDGET_LOW_REMAINING", 2,
                Domain.Validation.ValueObjects.ValidationStep.BudgetAvailability, Domain.Validation.ValueObjects.RuleLayer.Tenant,
                Domain.Validation.ValueObjects.Severity.Warning, new Dictionary<string, string> { ["pct"] = "0.12" }, [],
                new DateOnly(2025, 7, 1), null, "Little budget remains after this invoice.", "No action required."));
            return await db.SaveChangesAsync();
        });

        var refused = await t.PostAsync(inv.Id);
        refused.Status.Should().Be(CommandStatus.Refused);
        refused.Reason.Should().Contain("REVALIDATION_REQUIRED");
        (await t.GetAsync(inv.Id)).Status.Should().Be("Submitted");  // a new cycle is open

        await t.ApproveThroughAsync(inv.Id);                          // approval against the current fingerprint
        (await t.PostAsync(inv.Id)).IsAccepted.Should().BeTrue();
    }

    [Fact]
    public async Task ApprovalPreservesContentVersion()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(160_000m, "PO-2026-0451", (Police, 160_000m, 1));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var before = await t.GetAsync(inv.Id);
        (await t.Service<IApprovalAppService>().ApproveAsync(new InvoiceActionCommand(TenantDriver.Env(before.RowVersion), inv.Id), t.PoliceChief)).IsAccepted.Should().BeTrue();
        var after = await t.GetAsync(inv.Id);
        after.ContentVersion.Should().Be(before.ContentVersion);
        after.RowVersion.Should().NotBe(before.RowVersion);
        (await t.BudgetAsync(Police)).Held.Should().Be(0m);          // a fully liquidating PO invoice takes no reservation
    }

    [Fact]
    public async Task ResubmitDoesNotReuseOldApprovals()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(160_000m, "PO-2026-0451", (Police, 160_000m, 1));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var v = await t.GetAsync(inv.Id);
        (await t.Service<IApprovalAppService>().ApproveAsync(new InvoiceActionCommand(TenantDriver.Env(v.RowVersion), inv.Id), t.PoliceChief)).IsAccepted.Should().BeTrue();
        v = await t.GetAsync(inv.Id);
        (await t.Service<IInvoiceAppService>().WithdrawAsync(new ReasonedActionCommand(TenantDriver.Env(v.RowVersion), inv.Id, "fix"), t.Clerk)).IsAccepted.Should().BeTrue();
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var resubmitted = await t.GetAsync(inv.Id);
        resubmitted.ApprovalCycleId.Should().NotBeNull().And.NotBe(v.ApprovalCycleId!.Value);   // FA 7: Guid? has no NotBe(Guid?)
        resubmitted.Approvals.Should().ContainSingle(a => !a.IsActiveCycle);
        (await t.Service<IApprovalAppService>().GetQueueAsync(t.PoliceChief)).Should().Contain(q => q.InvoiceId == inv.Id);
    }

    [Fact]
    public async Task WithdrawReleasesEverything()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(164_800m, "PO-2026-0451", (Police, 164_800m, 1));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        (await t.BudgetAsync(Police)).Held.Should().Be(4_800m);
        var v = await t.GetAsync(inv.Id);
        (await t.Service<IInvoiceAppService>().WithdrawAsync(new ReasonedActionCommand(TenantDriver.Env(v.RowVersion), inv.Id, "wrong amount"), t.Clerk))
            .IsAccepted.Should().BeTrue();
        (await t.BudgetAsync(Police)).Held.Should().Be(0m);
        (await t.WithDbAsync(db => db.Encumbrances.SelectMany(e => e.Claims).CountAsync(c => c.Status == Domain.Ledger.Entities.ClaimStatus.Held))).Should().Be(0);
        (await t.WithDbAsync(db => db.Encumbrances.SelectMany(e => e.BillingClaims)
            .CountAsync(c => c.Status == Domain.Ledger.Entities.ClaimStatus.Held))).Should().Be(0);
        (await t.GetAsync(inv.Id)).Status.Should().Be("Draft");
        (await t.WithDbAsync(db => db.JournalEntries.CountAsync())).Should().Be(0);
        (await t.Service<IExplanationAppService>().GetAuditTrailAsync(inv.Id, t.Clerk)).Should().Contain(e => e.Action == "InvoiceWithdrawn");
    }

    [Fact]
    public async Task PostedInvoiceCannotBeWithdrawn()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(160_000m, "PO-2026-0451", (Police, 160_000m, 1));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        await t.ApproveThroughAsync(inv.Id);
        (await t.PostAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var before = await t.BudgetAsync(Police);
        var v = await t.GetAsync(inv.Id);
        (await t.Service<IInvoiceAppService>().WithdrawAsync(new ReasonedActionCommand(TenantDriver.Env(v.RowVersion), inv.Id, "late"), t.Clerk))
            .Status.Should().Be(CommandStatus.Refused);
        (await t.BudgetAsync(Police)).Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task SoftStopCanHoldButCannotPost()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(12_000m, null, ("101-6000-53100", 12_000m, null));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        (await t.BudgetAsync("101-6000-53100")).Held.Should().Be(12_000m);   // the 2,000 shortfall is held
        var v = await t.GetAsync(inv.Id);
        (await t.Service<IApprovalAppService>().ApproveAsync(new InvoiceActionCommand(TenantDriver.Env(v.RowVersion), inv.Id), t.FireChief))
            .Status.Should().Be(CommandStatus.Refused);                    // open Soft Stop
        (await t.PostAsync(inv.Id)).Status.Should().Be(CommandStatus.Refused);
    }

    [Fact]
    public async Task CumulativePoToleranceCannotBeSplit()
    {
        var t = await fixture.CreateTenantAsync();
        var full = await t.CreateAsync(160_000m, "PO-2026-0451", (Police, 160_000m, 1));
        (await t.SubmitAsync(full.Id)).IsAccepted.Should().BeTrue();
        await t.ApproveThroughAsync(full.Id);
        (await t.PostAsync(full.Id)).IsAccepted.Should().BeTrue();     // the PO is fully billed

        var a = await t.CreateAsync(5_000m, "PO-2026-0451", (Police, 5_000m, 1));   // 3.125% each, within tolerance
        var b = await t.CreateAsync(5_000m, "PO-2026-0451", (Police, 5_000m, 1));   // 6.25% together, over 5%
        t.Hooks.AfterEncumbranceRead = new SyncPoint(2);
        var results = await Task.WhenAll(Task.Run(() => t.SubmitAsync(a.Id)), Task.Run(() => t.SubmitAsync(b.Id)));
        t.Hooks.Reset();
        results.Count(r => r.IsAccepted).Should().Be(1);
        results.Single(r => !r.IsAccepted).Status.Should().BeOneOf(CommandStatus.Refused, CommandStatus.Conflict);
    }

    [Fact]
    public async Task DuplicateRaceHasOneWinner()
    {
        var t = await fixture.CreateTenantAsync();
        var svc = t.Service<IInvoiceAppService>();
        CreateInvoiceCommand Cmd(string number) => new(TenantDriver.Env(), number, SpringfieldData.AcmeId, SpringfieldData.Jun15, SpringfieldData.Jun15,
            SpringfieldData.Jun15, new DateOnly(2026, 7, 15), 10m, null, [new DistributionCommand("101-6000-53100", 10m, null)]);
        t.Hooks.AfterDuplicateCheck = new SyncPoint(2);
        var results = await Task.WhenAll(Task.Run(() => svc.CreateDraftAsync(Cmd(" ABC "), t.Clerk)), Task.Run(() => svc.CreateDraftAsync(Cmd("abc"), t.Clerk)));
        t.Hooks.Reset();
        results.Count(r => r.IsAccepted).Should().Be(1);
        results.Single(r => !r.IsAccepted).Reason.Should().Contain("already exists");
    }

    [Fact]
    public async Task PaymentHandoffReadiness()
    {
        var t = await fixture.CreateTenantAsync();                    // the fixture BusinessDate is 2026-07-15 = DueDate
        var inv = await t.CreateAsync(160_000m, "PO-2026-0451", (Police, 160_000m, 1));
        (await t.GetAsync(inv.Id)).ReadyForPaymentHandoff.Should().BeFalse();   // not Posted
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        await t.ApproveThroughAsync(inv.Id);
        (await t.PostAsync(inv.Id)).IsAccepted.Should().BeTrue();
        (await t.GetAsync(inv.Id)).ReadyForPaymentHandoff.Should().BeTrue();
        var v = await t.GetAsync(inv.Id);
        (await t.Service<IInvoiceAppService>().SetPaymentHoldAsync(new PaymentHoldCommand(TenantDriver.Env(v.RowVersion), inv.Id, true), t.FinanceDirector))
            .IsAccepted.Should().BeTrue();
        (await t.GetAsync(inv.Id)).ReadyForPaymentHandoff.Should().BeFalse();
    }
}
