using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.DomainServices.Rules;
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Tests.Rules;

public class Step5And6RulesTests
{
    private static BudgetSnapshot Budget(decimal amended = 375000, decimal actuals = 132000,
        decimal encumbered = 96000, decimal held = 0, decimal own = 0, int year = 2026) =>
        new(true, new(amended), new(actuals), new(encumbered), new(held),
            new(amended - actuals - encumbered - held), new(own), year);

    private static DistributionSnapshot Line(int no, decimal amount, BudgetSnapshot? budget = null,
        EncumbranceSnapshot? po = null, BudgetControl control = BudgetControl.Hard,
        string account = "701-6000-53100-G-COPS-26") =>
        new(no, AccountCode.Parse(account), new(amount), new(true, true, "Active"),
            new("701", "Grants", FundKind.Governmental, control, GrantRule.Required, FundRestriction.Allowed, true),
            null, budget ?? Budget(), po);

    private static EncumbranceSnapshot Po(decimal remaining = 96000, decimal authorized = 100000,
        decimal posted = 0, decimal billingClaims = 0, decimal liquidationClaims = 0, decimal own = 0) =>
        new("PO-1/1", new(remaining), true, new(authorized), new(posted), new(billingClaims),
            OtherLiquidationClaims: new(liquidationClaims), OwnLiquidationClaim: new(own));

    private static ValidationSubject Subject(params DistributionSnapshot[] lines) => new(
        new("INV-1", 1, "AP_INVOICE", new(2026, 6, 15), new(lines.Sum(x => x.Amount.Amount)),
            new(Guid.NewGuid(), "Vendor", false, true), lines.Any(x => x.Encumbrance != null), false,
            new(Guid.NewGuid())), lines, [], [], true, null,
        new(new("2100"), new("2900"), new("5900"), new("0000")));

    private static RuleDefinition Def(string id, string tolerance = "0.05") => new(id, 1,
        id == "PO_LIQUIDATION" ? ValidationStep.EncumbranceImpact : ValidationStep.BudgetAvailability,
        RuleLayer.Core, null, new Dictionary<string, string> { ["pct"] = "0.10", ["tolerance_pct"] = tolerance },
        [ApproverRole.BudgetOfficer], new(2026, 1, 1), null, "Budget rule", "Review budget");

    [Fact]
    public void Shared_budget_checks_invoice_total_once()
    {
        var result = new BudgetAvailabilityRule().Evaluate(Subject(Line(2, 80000), Line(1, 80000)), Def("BUDGET_AVAILABILITY"));
        result.Should().ContainSingle();
        result[0].Severity.Should().Be(Severity.HardStop);
        result[0].Computed["overage"].Should().Be("13,000.00");
        result[0].Computed["availableAfter"].Should().Be("-13,000.00");
    }

    [Fact]
    public void Own_budget_hold_is_added_back_once_per_budget()
    {
        var b = Budget(held: 100000, own: 100000);
        new BudgetAvailabilityRule().Evaluate(Subject(Line(1, 50000, b), Line(2, 50000, b)), Def("BUDGET_AVAILABILITY"))
            .Should().BeEmpty();
    }

    [Fact]
    public void Different_fiscal_years_do_not_share_budget()
    {
        new BudgetAvailabilityRule().Evaluate(Subject(Line(1, 80000), Line(2, 80000, Budget(year: 2027))), Def("BUDGET_AVAILABILITY"))
            .Should().BeEmpty();
    }

    [Fact]
    public void Soft_budget_returns_soft_stop_and_missing_budget_hard_stop()
    {
        new BudgetAvailabilityRule().Evaluate(Subject(Line(1, 160000, control: BudgetControl.Soft)), Def("BUDGET_AVAILABILITY"))
            .Should().ContainSingle(x => x.Severity == Severity.SoftStop);
        new BudgetAvailabilityRule().Evaluate(Subject(Line(1, 1, BudgetSnapshot.Missing, control: BudgetControl.Soft)), Def("BUDGET_AVAILABILITY"))
            .Should().ContainSingle(x => x.Severity == Severity.HardStop);
    }

    [Fact]
    public void Allocation_shares_remainder_in_line_order_and_ignores_cached_allocation()
    {
        var p = Po(liquidationClaims: 16000);
        var s = Subject(Line(2, 60000, po: p), Line(1, 60000, po: p) with { AllocatedLiquidation = new(999) });
        var allocation = BudgetAllocation.Allocate(s);
        allocation.Select(x => x.Distribution.LineNo).Should().Equal(1, 2);
        allocation.Select(x => x.LiquidationAmount.Amount).Should().Equal(60000m, 20000m);
        allocation.Select(x => x.RequiredNewBudget.Amount).Should().Equal(0m, 40000m);
        BudgetAllocation.Allocate(s).Should().Equal(allocation);
    }

    [Theory]
    [InlineData("Submitted")]
    [InlineData("Approved")]
    public void Active_invoice_uses_own_claim_not_newly_free_remainder(string status)
    {
        var s = Subject(Line(1, 80000, po: Po(own: 20000)));
        s = s with { Transaction = s.Transaction with { Status = status } };
        BudgetAllocation.Allocate(s).Single().LiquidationAmount.Should().Be(new Money(20000));
    }

    [Fact]
    public void Budget_uses_total_allocated_liquidation_only_once()
    {
        var b = Budget(100000, 0, 96000);
        var p = Po();
        new BudgetAvailabilityRule().Evaluate(Subject(Line(1, 50000, b, p), Line(2, 50000, b, p)), Def("BUDGET_AVAILABILITY"))
            .Should().BeEmpty();
        var result = new BudgetAvailabilityRule().Evaluate(Subject(Line(1, 60000, b, p), Line(2, 60000, b, p)), Def("BUDGET_AVAILABILITY"));
        result.Should().ContainSingle(x => x.Computed["overage"] == "20,000.00");
    }

    [Theory]
    [InlineData(18500, "6.00", 1)]
    [InlineData(20000, "0.00", 1)]
    [InlineData(17500, "", 0)]
    [InlineData(21000, "", 0)]
    public void Low_remaining_groups_lines_and_warns_only_below_threshold(decimal amount, string pct, int count)
    {
        var b = Budget(25000, 5000, 0);
        var result = new BudgetLowRemainingRule().Evaluate(Subject(Line(1, amount / 2, b), Line(2, amount / 2, b)), Def("BUDGET_LOW_REMAINING"));
        result.Should().HaveCount(count);
        if (count > 0) { result[0].Computed["remainingPct"].Should().Be(pct); result[0].Severity.Should().Be(Severity.Warning); }
    }

    [Theory]
    [InlineData(5000, "0.05", Severity.Warning)]
    [InlineData(5000.01, "0.05", Severity.HardStop)]
    [InlineData(5000.01, "0.06", Severity.Warning)]
    public void Tolerance_uses_authorized_amount_and_effective_parameter(decimal amount, string tolerance, Severity expected)
    {
        var p = Po(remaining: 1000, posted: 100000);
        new PoLiquidationRule().Evaluate(Subject(Line(1, amount, po: p)), Def("PO_LIQUIDATION", tolerance))
            .Should().ContainSingle(x => x.Severity == expected);
    }

    [Fact]
    public void Full_billing_claims_and_all_lines_are_counted_even_when_liquidation_covers_everything()
    {
        var p = Po(remaining: 100000, posted: 90000, billingClaims: 5000);
        var result = new PoLiquidationRule().Evaluate(Subject(Line(1, 6000, po: p), Line(2, 6000, po: p)), Def("PO_LIQUIDATION"));
        result.Should().ContainSingle(x => x.Severity == Severity.HardStop);
        result[0].Computed["excessPct"].Should().Be("7.00");
    }

    [Fact]
    public void Group_total_is_authoritative_not_repeated_snapshot_invoice_amount()
    {
        var p = Po(remaining: 100000) with { CurrentInvoicePoAmount = new(100000) };
        new PoLiquidationRule().Evaluate(Subject(Line(1, 50000, po: p), Line(2, 50000, po: p)), Def("PO_LIQUIDATION"))
            .Should().ContainSingle(x => x.Severity == Severity.Allowed);
    }

    [Fact]
    public void Closed_and_zero_authorized_po_fail_closed()
    {
        // A line of a PO-backed invoice without a PO line is refused by the pipeline (VALIDATION_INPUT).
        foreach (var p in new[] { Po() with { IsOpen = false }, Po(authorized: 0) })
        {
            BudgetAllocation.Allocate(Subject(Line(1, 1, po: p))).Single().LiquidationAmount.Should().Be(Money.Zero);
            new PoLiquidationRule().Evaluate(Subject(Line(1, 1, po: p)), Def("PO_LIQUIDATION"))
                .Should().ContainSingle(x => x.Severity == Severity.HardStop);
        }
    }

    [Fact]
    public void Conflicting_budget_and_po_snapshots_and_po_account_mismatch_fail_closed()
    {
        var p = Po();
        var subjects = new[] {
            Subject(Line(1, 1), Line(2, 1, Budget(400000))),
            Subject(Line(1, 1, po:p), Line(2, 1, po:p with { Remaining = new(95000) })),
            Subject(Line(1, 1, po:p), Line(2, 1, po:p, account:"701-3000-53100-G-COPS-26"))
        };
        foreach (var s in subjects)
        {
            BudgetAllocation.Allocate(s).Should().OnlyContain(x => x.Error != null && x.LiquidationAmount == Money.Zero);
            new BudgetAvailabilityRule().Evaluate(s, Def("BUDGET_AVAILABILITY")).Should().OnlyContain(x => x.Severity == Severity.HardStop);
        }
    }
    [Fact]
    public void Non_po_invoice_cannot_receive_liquidation_credit()
    {
        var s = Subject(Line(1, 100, po: Po()));
        s = s with { Transaction = s.Transaction with { IsPoBacked = false } };
        BudgetAllocation.Allocate(s).Should().ContainSingle(x => x.Error != null && x.LiquidationAmount == Money.Zero);
        new PoLiquidationRule().Evaluate(s, Def("PO_LIQUIDATION")).Should().ContainSingle(x => x.Severity == Severity.HardStop);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Nonpositive_distribution_amount_is_invalid(decimal amount)
    {
        BudgetAllocation.Allocate(Subject(Line(1, amount))).Should().ContainSingle(x => x.Error != null);
    }

    [Fact]
    public void Negative_budget_balances_and_invalid_fiscal_year_fail_closed()
    {
        var invalid = new[] { Budget(amended: -1), Budget(actuals: -1), Budget(encumbered: -1),
            Budget(held: -1), Budget(year: 1), Budget(year: 10000) };
        foreach (var b in invalid)
            new BudgetAvailabilityRule().Evaluate(Subject(Line(1, 1, b)), Def("BUDGET_AVAILABILITY"))
                .Should().ContainSingle(x => x.Severity == Severity.HardStop);
    }

    [Theory]
    [InlineData(100001, 0)]
    [InlineData(96000, 101)]
    public void Po_remaining_cannot_exceed_authorization_or_own_claim_exceed_invoice(decimal remaining, decimal own)
    {
        var s = Subject(Line(1, 50, po: Po(remaining: remaining, own: own)), Line(2, 50, po: Po(remaining: remaining, own: own)));
        BudgetAllocation.Allocate(s).Should().OnlyContain(x => x.Error != null && x.LiquidationAmount == Money.Zero);
        new PoLiquidationRule().Evaluate(s, Def("PO_LIQUIDATION")).Should().ContainSingle(x => x.Severity == Severity.HardStop);
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("1.01")]
    public void Invalid_tolerance_rejected_even_without_po(string tolerance)
    {
        var act = () => new PoLiquidationRule().Evaluate(Subject(Line(1, 1)), Def("PO_LIQUIDATION", tolerance));
        act.Should().Throw<GovErp.Domain.Validation.Exceptions.ValidationException>();
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("1.01")]
    public void Invalid_low_remaining_percentage_rejected(string pct)
    {
        var def = new RuleDefinition("BUDGET_LOW_REMAINING", 1, ValidationStep.BudgetAvailability,
            RuleLayer.Core, Severity.Warning, new Dictionary<string, string> { ["pct"] = pct }, [],
            new(2026, 1, 1), null, "Low remaining", "Review");
        var act = () => new BudgetLowRemainingRule().Evaluate(Subject(Line(1, 1)), def);
        act.Should().Throw<GovErp.Domain.Validation.Exceptions.ValidationException>();
    }
}

