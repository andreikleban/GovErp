using GovErp.Application.Web.Budget;
using GovErp.Application.Web.Budget.Commands;
using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Invoices;
using GovErp.Application.Web.Invoices.Commands;
using GovErp.Application.Web.Posting;
using GovErp.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Application.Web.Tests.Integration;

[Collection("sql")]
public sealed class ConcurrencyAndAtomicityTests(SqlServerFixture fixture)
{
    private const string Fire = "701-6000-53100-G-COPS-26";
    private const string Police = "701-3000-53100-G-COPS-26";

    [Fact]
    public async Task OwnReservationIsNotChargedTwice()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(100_000m, null, (Fire, 100_000m, null));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        (await t.BudgetAsync(Fire)).Held.Should().Be(100_000m);
        await t.ApproveThroughAsync(inv.Id);                          // перевалидации на Approve не видят дефицита
        (await t.PostAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var line = await t.BudgetAsync(Fire);
        (line.Available, line.Held, line.Actuals).Should().Be((47_000m, 0m, 232_000m));
    }

    [Fact]
    public async Task SameBudgetAcrossDistributions()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(160_000m, null, (Fire, 80_000m, null), (Fire, 80_000m, null));
        var r = await t.SubmitAsync(inv.Id);
        r.Status.Should().Be(CommandStatus.Refused);
        r.Value!.LastEvaluation!.Outcomes.Single(o => o.RuleId == "BUDGET_AVAILABILITY").Computed["overage"].Should().Be("13,000.00");
        (await t.BudgetAsync(Fire)).Held.Should().Be(0m);
    }

    [Fact]
    public async Task ParallelBudgetSubmits()
    {
        var t = await fixture.CreateTenantAsync();
        var a = await t.CreateAsync(100_000m, null, (Fire, 100_000m, null));
        var b = await t.CreateAsync(100_000m, null, (Fire, 100_000m, null));
        t.Hooks.AfterBudgetRead = new SyncPoint(2);
        var results = await Task.WhenAll(Task.Run(() => t.SubmitAsync(a.Id)), Task.Run(() => t.SubmitAsync(b.Id)));
        t.Hooks.Reset();
        results.Count(r => r.IsAccepted).Should().Be(1);
        results.Single(r => !r.IsAccepted).Status.Should().BeOneOf(CommandStatus.Refused, CommandStatus.Conflict);
        (await t.BudgetAsync(Fire)).Held.Should().Be(100_000m);
    }

    [Fact]
    public async Task ParallelPoClaims()
    {
        var t = await fixture.CreateTenantAsync();
        var amend = await t.Service<IBudgetAppService>().AmendAsync(
            new AmendBudgetCommand(TenantDriver.Env(), Police, 2026, -240_000m, "BA-T-ZERO", SpringfieldData.Jun15), t.BudgetOfficer);
        amend.IsAccepted.Should().BeTrue(amend.Reason);                // свободный бюджет Police = 0
        var a = await t.CreateAsync(160_000m, "PO-2026-0451", (Police, 160_000m, 1));
        var b = await t.CreateAsync(160_000m, "PO-2026-0451", (Police, 160_000m, 1));
        t.Hooks.AfterEncumbranceRead = new SyncPoint(2);
        var results = await Task.WhenAll(Task.Run(() => t.SubmitAsync(a.Id)), Task.Run(() => t.SubmitAsync(b.Id)));
        t.Hooks.Reset();
        results.Count(r => r.IsAccepted).Should().Be(1);
        var claims = await t.WithDbAsync(db => db.Encumbrances.Where(e => e.PoLineRef == "PO-2026-0451/1").SelectMany(e => e.Claims)
            .CountAsync(c => c.Status == Domain.Ledger.Entities.ClaimStatus.Held));
        claims.Should().Be(1);
    }

    [Fact]
    public async Task PoPostingTotals()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(160_000m, "PO-2026-0451", (Police, 160_000m, 1));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        await t.ApproveThroughAsync(inv.Id);
        (await t.PostAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var line = await t.BudgetAsync(Police);
        (line.Actuals, line.Encumbered, line.Available).Should().Be((260_000m, 0m, 240_000m));
        (await t.Service<IPostingAppService>().GetJournalAsync(inv.Id, t.Clerk)).Should().HaveCount(4);
    }

    [Fact]
    public async Task MixedPostingTotals()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(164_800m, "PO-2026-0451", (Police, 164_800m, 1));   // 3% сверх PO — в допуске
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        await t.ApproveThroughAsync(inv.Id);
        (await t.PostAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var line = await t.BudgetAsync(Police);
        (line.Actuals, line.Encumbered, line.Available).Should().Be((264_800m, 0m, 235_200m));
    }

    [Fact]
    public async Task AtomicSubmitFailure()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(30_000m, null, ("101-6000-53100", 12_000m, null), ("202-4000-53100", 8_000m, null), ("501-5000-53100", 10_000m, null));
        t.Hooks.FailOnSecondLookupOf = "202-4000-53100";               // первое чтение — сборщик снимка, второе — резервирование
        await FluentActions.Awaiting(() => t.SubmitAsync(inv.Id)).Should().ThrowAsync<InvalidOperationException>();
        t.Hooks.Reset();
        (await t.GetAsync(inv.Id)).Status.Should().Be("Draft");
        foreach (var account in new[] { "101-6000-53100", "202-4000-53100", "501-5000-53100" })
        {
            (await t.BudgetAsync(account)).Held.Should().Be(0m);
        }
    }

    [Fact]
    public async Task AtomicPostFailure()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(160_000m, "PO-2026-0451", (Police, 160_000m, 1));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        await t.ApproveThroughAsync(inv.Id);
        var before = await t.BudgetAsync(Police);
        t.Hooks.FailJournalWrite = true;
        await FluentActions.Awaiting(() => t.PostAsync(inv.Id)).Should().ThrowAsync<InvalidOperationException>();
        t.Hooks.Reset();
        (await t.GetAsync(inv.Id)).Status.Should().Be("Approved");
        (await t.BudgetAsync(Police)).Should().BeEquivalentTo(before);
        (await t.WithDbAsync(db => db.Encumbrances.Where(e => e.PoLineRef == "PO-2026-0451/1").Select(e => e.Liquidated).SingleAsync()))
            .Should().Be(Money.Zero);
    }

    [Fact]
    public async Task SameCommandRepeated()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(160_000m, "PO-2026-0451", (Police, 160_000m, 1));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        await t.ApproveThroughAsync(inv.Id);
        var env = TenantDriver.Env((await t.GetAsync(inv.Id)).RowVersion);
        var first = await t.PostAsync(inv.Id, env);
        var second = await t.PostAsync(inv.Id, env);
        first.IsAccepted.Should().BeTrue();
        second.Should().BeEquivalentTo(first);
        (await t.WithDbAsync(db => db.JournalEntries.CountAsync(j => j.SourceRef == inv.Reference))).Should().Be(1);
        (await t.WithDbAsync(db => db.CommandReceipts.CountAsync(r => r.CommandId == env.CommandId))).Should().Be(1);
    }

    [Fact]
    public async Task SameCommandDifferentPayload()
    {
        var t = await fixture.CreateTenantAsync();
        var svc = t.Service<IInvoiceAppService>();
        var env = TenantDriver.Env();
        CreateInvoiceCommand Cmd(string number) => new(env, number, SpringfieldData.AcmeId, SpringfieldData.Jun15, SpringfieldData.Jun15,
            SpringfieldData.Jun15, new DateOnly(2026, 7, 15), 10m, null, [new DistributionCommand("101-6000-53100", 10m, null)]);
        (await svc.CreateDraftAsync(Cmd("P-1"), t.Clerk)).IsAccepted.Should().BeTrue();
        var second = await svc.CreateDraftAsync(Cmd("P-2"), t.Clerk);
        second.Status.Should().Be(CommandStatus.Conflict);
        second.Retryable.Should().BeFalse();
    }
}
