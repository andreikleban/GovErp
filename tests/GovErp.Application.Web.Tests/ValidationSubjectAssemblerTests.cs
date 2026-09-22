using GovErp.Application.Web.Validation;
using GovErp.Domain.Payables.Entities;
using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.ValueObjects;
using GovErp.Infrastructure.Seed;
using Microsoft.Extensions.Options;

namespace GovErp.Application.Web.Tests;

public class ValidationSubjectAssemblerTests
{
    private static readonly AccountCode Fire = AccountCode.Parse("701-6000-53100-G-COPS-26");

    internal static ValidationSubjectAssembler Assembler(SpringfieldData d, IEnumerable<VendorInvoice>? invoices = null,
        IEnumerable<Domain.Validation.Entities.EvaluationRecord>? evaluations = null) =>
        new(new InMemoryFundRepository(d.Funds), new InMemoryGrantRepository(d.Grants), new InMemoryAccountCombinationRepository(d.Combinations),
            new InMemoryBudgetLineRepository(d.BudgetLines), new InMemoryEncumbranceRepository(d.Encumbrances),
            new InMemoryFiscalPeriodRepository(d.Periods), new InMemoryVendorRepository(d.Vendors),
            new InMemoryVendorInvoiceRepository(invoices ?? []), new InMemoryEvaluationRecordRepository(evaluations ?? []),
            Options.Create(new PostingOptions()));

    [Fact]
    public async Task Exercise_invoice_yields_exercise_snapshot_and_hard_stop()
    {
        var d = SpringfieldData.Create();
        var inv = d.NonPoExerciseInvoice();
        var s = await Assembler(d).BuildAsync(inv, SpringfieldData.Jun15);
        s.Transaction.ContentVersion.Should().Be(inv.ContentVersion);
        s.Transaction.Status.Should().Be("Draft");
        s.Distributions.Single().Budget.Available.Should().Be(Money.Of(147_000m));
        s.Distributions.Single().Budget.FiscalYear.Should().Be(2026);
        s.PeriodIsOpen.Should().BeTrue();

        var record = new ValidationPipeline(RuleCatalog.Default).Evaluate(s, d.Rules, EvaluationTrigger.Manual, SpringfieldData.ClerkId, DateTimeOffset.UtcNow);
        record.Overall.Should().Be(Severity.HardStop);
        record.Outcomes.Single(o => o.RuleId == "BUDGET_AVAILABILITY").Computed["overage"].Should().Be("13,000.00");
    }

    [Fact]
    public async Task Distributions_on_one_budget_key_share_one_snapshot()
    {
        var d = SpringfieldData.Create();
        var inv = new VendorInvoice("V-2", SpringfieldData.AcmeId, SpringfieldData.Jun15, SpringfieldData.Jun15, SpringfieldData.Jun15,
            SpringfieldData.Jul15, Money.Of(160_000m), null, SpringfieldData.ClerkId, DateTimeOffset.UtcNow);
        inv.AddDistribution(Fire, Money.Of(80_000m), null);
        inv.AddDistribution(Fire, Money.Of(80_000m), null);
        var s = await Assembler(d).BuildAsync(inv, SpringfieldData.Jun15);
        s.Distributions[0].Budget.Should().BeSameAs(s.Distributions[1].Budget);
    }

    [Fact]
    public async Task Own_reservation_is_reported_as_own_held()
    {
        var d = SpringfieldData.Create();
        var inv = d.NonPoExerciseInvoice();
        d.BudgetLines.Single(l => l.Account == Fire).Reserve(inv.Id, inv.ContentVersion, Money.Of(100_000m), inv.Reference);
        var budget = (await Assembler(d).BuildAsync(inv, SpringfieldData.Jun15)).Distributions.Single().Budget;
        budget.Held.Should().Be(Money.Of(100_000m));
        budget.OwnHeld.Should().Be(Money.Of(100_000m));
    }

    [Fact]
    public async Task Po_backed_snapshot_separates_own_and_other_claims()
    {
        var d = SpringfieldData.Create();
        var inv = d.PoBackedInvoice();
        var enc = d.Encumbrances.Single(e => e.PoLineRef == "PO-2026-0451/1");
        enc.Claim(Guid.NewGuid(), 1, Money.Of(10_000m));
        enc.ClaimBilling(Guid.NewGuid(), 1, Money.Of(10_000m), .05m);
        var e = (await Assembler(d).BuildAsync(inv, SpringfieldData.Jun15)).Distributions.Single().Encumbrance!;
        e.PoLineRef.Should().Be("PO-2026-0451/1");
        e.AuthorizedPoAmount.Should().Be(Money.Of(160_000m));
        e.OtherLiquidationClaims.Should().Be(Money.Of(10_000m));
        e.OtherActiveInvoiceClaims.Should().Be(Money.Of(10_000m));
        e.CurrentInvoicePoAmount.Should().Be(Money.Of(160_000m));
        e.ClaimableForInvoice.Should().Be(Money.Of(150_000m));
    }

    [Fact]
    public async Task Duplicate_and_closed_period_are_detected()
    {
        var d = SpringfieldData.Create();
        var first = d.NonPoExerciseInvoice(" v-7781 ");
        (await Assembler(d, [first]).BuildAsync(d.NonPoExerciseInvoice("V-7781"), SpringfieldData.Jun15)).Transaction.IsDuplicate.Should().BeTrue();
        (await Assembler(d).BuildAsync(d.NonPoExerciseInvoice(date: new DateOnly(2026, 5, 20)), SpringfieldData.Jun15)).PeriodIsOpen.Should().BeFalse();
    }

    [Fact]
    public async Task Active_overrides_bring_their_evaluations_as_evidence()
    {
        var d = SpringfieldData.Create();
        var inv = d.NonPoExerciseInvoice();
        d.BudgetLines.Single(l => l.Account == Fire).Amend(Money.Of(13_000m), "BA-1", SpringfieldData.Jun15);
        var pipeline = new ValidationPipeline(RuleCatalog.Default);
        var first = pipeline.Evaluate(await Assembler(d).BuildAsync(inv, SpringfieldData.Jun15), d.Rules, EvaluationTrigger.Submit,
            SpringfieldData.ClerkId, DateTimeOffset.UtcNow);
        inv.Submit(first.Id, first.RuleFingerprint, [new(Domain.Payables.Entities.ApproverRole.DepartmentHead, new DepartmentCode("6000"))], []);
        var soft = first.Outcomes.Single(o => o.RuleId == "PROCUREMENT_THRESHOLD");
        inv.Override(new OverrideTarget(first.Id, soft.OutcomeRef, soft.RuleId, soft.RuleVersion, soft.DistributionLine, inv.ContentVersion,
            inv.ApprovalCycleId!.Value), Domain.Payables.Entities.ApproverRole.FinanceDirector, UserId.New(), "Sole source on file", DateTimeOffset.UtcNow);

        var s = await Assembler(d, evaluations: [first]).BuildAsync(inv, SpringfieldData.Jun15);
        s.OverridesSoFar.Should().ContainSingle(o => o.OutcomeRef == soft.OutcomeRef && o.Role == Domain.Validation.ValueObjects.ApproverRole.FinanceDirector);
        s.PreviousEvaluations.Should().ContainSingle(e => e.EvaluationId == first.Id);
        pipeline.Evaluate(s, d.Rules, EvaluationTrigger.Manual, SpringfieldData.ClerkId, DateTimeOffset.UtcNow)
            .Outcomes.Single(o => o.RuleId == "PROCUREMENT_THRESHOLD").IsOverridden.Should().BeTrue();
    }
}
