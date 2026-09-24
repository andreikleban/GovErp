using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Tests;

public class DistributionSnapshotTests
{
    [Fact]
    public void Budget_rejects_inconsistent_available_and_recomputes_after_with()
    {
        var invalid = () => new BudgetSnapshot(true, Money.Of(100), Money.Of(10), Money.Of(20), Money.Of(30), Money.Of(99));
        invalid.Should().Throw<ArgumentException>();
        var changed = SubjectBuilder.Budget(100, 10, 20, 30) with { Actuals = Money.Of(15) };
        changed.Available.Should().Be(Money.Of(35));
    }

    [Fact]
    public void Previous_outcomes_are_copied_and_override_retains_exact_provenance()
    {
        var example = SubjectBuilder.Exercise();
        var outcome = RuleOutcome.From(new GovErp.Domain.Validation.Entities.RuleDefinition(
            "RULE", 1, ValidationStep.TransactionPurpose, RuleLayer.Core, Severity.SoftStop,
            new Dictionary<string, string>(), [ApproverRole.FinanceDirector], SubjectBuilder.June15, null,
            "message", "resolution", true), Severity.SoftStop, null,
            new Dictionary<string, string>(), new Dictionary<string, string>(), "TEST.STOP");
        var previous = new List<RuleOutcome> { outcome };
        var id = Guid.NewGuid();
        var subject = new ValidationSubject(example.Transaction, example.Distributions, [], [], true, null,
            SubjectBuilder.Accounts, PreviousOutcomes: previous, PreviousEvaluationId: id);
        var copy = subject with { PreviousOutcomes = previous };
        previous.Clear();
        subject.PreviousOutcomes.Should().ContainSingle().Which.Should().Be(outcome);
        copy.PreviousOutcomes.Should().ContainSingle();
        var value = new OverrideSnapshot("RULE", SubjectBuilder.Approver, "reason", 1, null, 1,
            example.Transaction.ApprovalCycleId, "fp", id, ApproverRole.FinanceDirector, outcome.OutcomeRef);
        value.EvaluationId.Should().Be(subject.PreviousEvaluationId!.Value);
        value.OutcomeRef.Should().Be(outcome.OutcomeRef);
        value.DistributionLine.Should().BeNull();
    }

    [Fact]
    public void Unallocated_po_lines_cannot_independently_spend_the_same_remaining()
    {
        var po = new EncumbranceSnapshot("PO/1", Money.Of(96_000), true);
        var first = SubjectBuilder.Distribution(1, "101-3000-53100", 80_000, encumbrance: po);
        var second = first with { LineNo = 2 };
        first.LiquidationAmount.Should().Be(Money.Zero);
        second.AmountToCheck.Should().Be(Money.Of(80_000));
    }

    [Theory]
    [InlineData(160000, 160000, 0)]
    [InlineData(100000, 96000, 4000)]
    [InlineData(100000, 0, 100000)]
    public void Uses_explicit_pipeline_allocation(decimal amount, decimal allocated, decimal required)
    {
        var d = SubjectBuilder.Distribution(1, "101-3000-53100", amount,
            encumbrance: new("PO/1", Money.Of(160000), true), allocatedLiquidation: allocated);
        d.LiquidationAmount.Should().Be(Money.Of(allocated));
        d.Excess.Should().Be(Money.Of(required));
        d.RequiredNewBudget.Should().Be(d.AmountToCheck);
    }

    [Fact]
    public void Own_hold_is_available_to_this_invoice()
    {
        var budget = SubjectBuilder.Budget(375000, 132000, 96000, 100000, ownHeld: 100000);
        var d = SubjectBuilder.Distribution(1, "101-3000-53100", 100000, budget: budget);
        budget.Available.Should().Be(Money.Of(47000));
        budget.AvailableForThisInvoice.Should().Be(Money.Of(147000));
        d.ProjectedAvailable.Should().Be(Money.Of(47000));
    }

    [Fact]
    public void Billing_tolerance_is_separate_from_liquidation_claims()
    {
        var po = new EncumbranceSnapshot("PO/1", Money.Of(96000), true,
            Money.Of(160000), Money.Of(64000), Money.Of(4000), Money.Of(100000), Money.Of(20000));
        po.ClaimableForInvoice.Should().Be(Money.Of(76000));
        po.CumulativePoExcessPct.Should().Be(0.05m);
        var invalid = po with { AuthorizedPoAmount = Money.Zero };
        var calculate = () => invalid.CumulativePoExcessPct;
        calculate.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Subject_copies_constructor_and_with_expression_collections()
    {
        var example = SubjectBuilder.Exercise();
        var lines = example.Distributions.ToList();
        var roles = new List<ApproverRole> { ApproverRole.DepartmentHead };
        var overrides = new List<OverrideSnapshot> { new("RULE", SubjectBuilder.Clerk, "reason") };
        var approvals = new List<ApprovalSnapshot> { new(ApproverRole.DepartmentHead, SubjectBuilder.Approver,
            new DepartmentCode("6000"), 1, example.Transaction.ApprovalCycleId, "fp", Guid.NewGuid()) };
        var subject = new ValidationSubject(example.Transaction, lines, roles, overrides, true, null,
            SubjectBuilder.Accounts, approvals);
        var copy = subject with { Distributions = lines, ApprovalsSoFar = roles, OverridesSoFar = overrides, DetailedApprovals = approvals };
        lines.Clear(); roles.Clear(); overrides.Clear(); approvals.Clear();
        foreach (var snapshot in new[] { subject, copy })
        {
            snapshot.Distributions.Should().HaveCount(1);
            snapshot.ApprovalsSoFar.Should().HaveCount(1);
            snapshot.OverridesSoFar.Should().HaveCount(1);
            snapshot.DetailedApprovals.Should().HaveCount(1);
            var mutate = () => ((IList<DistributionSnapshot>)snapshot.Distributions).Clear();
            mutate.Should().Throw<NotSupportedException>();
        }
    }

    [Fact]
    public void Builder_defaults_to_correct_fiscal_dates_and_preserves_previous_builds()
    {
        var builder = new SubjectBuilder();
        var first = builder.Build();
        builder.With(SubjectBuilder.Distribution(2, "101-3000-53100", 10));
        first.Distributions.Should().HaveCount(1);
        first.Transaction.InvoiceDate.Should().Be(new DateOnly(2026, 6, 15));
        first.Transaction.ServiceDate.Should().Be(SubjectBuilder.June15);
        first.Transaction.PostingDate.Should().Be(SubjectBuilder.June15);
        first.Distributions[0].Budget.FiscalYear.Should().Be(2026);
        first.Transaction.ContentVersion.Should().Be(1);
        first.Transaction.Status.Should().Be("Draft");
    }
}
