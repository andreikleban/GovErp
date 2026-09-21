using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Ledger.Exceptions;
namespace GovErp.Domain.Ledger.Tests;

public class LifecycleTests
{
    private static readonly AccountCode Account = AccountCode.Parse("701-6000-53100-G-COPS-26");
    private static BudgetLine Budget() => new(Account, new FiscalYear(2026), BudgetControlMode.Hard, Money.Of(500000));
    private static Encumbrance Po() => new("PO/1", Account, Money.Of(96000), Money.Of(100000), Money.Zero);

    [Fact]
    public void Own_hold_is_only_for_current_invoice_and_version()
    {
        var b = Budget(); var id = Guid.NewGuid();
        b.Reserve(id, 1, Money.Of(100000), "INV");
        b.OwnHeld(id, 1).Should().Be(Money.Of(100000));
        b.OwnHeld(id, 2).Should().Be(Money.Zero);
        b.OwnHeld(Guid.NewGuid(), 1).Should().Be(Money.Zero);
        b.AvailableForInvoice(id, 1).Should().Be(Money.Of(500000));
        b.ChangeStamp.Should().Be(1);
    }

    [Fact]
    public void Mixed_post_counts_full_invoice_exactly_once()
    {
        var b = Budget(); b.RecordActuals(Money.Of(100000)); b.RecordEncumbrance(Money.Of(96000));
        var id = Guid.NewGuid(); var r = b.Reserve(id, 1, Money.Of(4000), "INV");
        b.Commit(r.ReservationId!.Value, id, 1); b.RecordLiquidation(Money.Of(96000));
        b.Actuals.Should().Be(Money.Of(200000)); b.Encumbered.Should().Be(Money.Zero);
        b.Held.Should().Be(Money.Zero); b.Available.Should().Be(Money.Of(300000));
    }

    [Fact]
    public void Wrong_owner_or_version_cannot_commit_and_failure_preserves_state()
    {
        var b = Budget(); var id = Guid.NewGuid(); var r = b.Reserve(id, 1, Money.Of(10), "INV");
        var stamp = b.ChangeStamp;
        Action wrongOwner = () => b.Commit(r.ReservationId!.Value, Guid.NewGuid(), 1);
        Action wrongVersion = () => b.Release(r.ReservationId!.Value, id, 2);
        wrongOwner.Should().Throw<LedgerException>(); wrongVersion.Should().Throw<LedgerException>();
        b.ChangeStamp.Should().Be(stamp); b.Held.Should().Be(Money.Of(10)); b.Actuals.Should().Be(Money.Zero);
    }

    [Fact]
    public void Competing_claims_cannot_capture_same_remaining()
    {
        var e = Po(); var id = Guid.NewGuid(); var claim = e.Claim(id, 1, Money.Of(96000));
        e.ClaimableForInvoice(id, 1).Should().Be(Money.Of(96000));
        e.ClaimableForInvoice(id, 2).Should().Be(Money.Zero);
        var stamp = e.ChangeStamp;
        Action second = () => e.Claim(Guid.NewGuid(), 1, Money.Of(96000));
        second.Should().Throw<LedgerException>(); e.ChangeStamp.Should().Be(stamp);
        e.ConsumeClaim(claim, id, 1); e.Remaining.Should().Be(Money.Zero);
        e.Liquidated.Should().Be(Money.Of(96000)); e.Claims.Single().Status.Should().Be(ClaimStatus.Consumed);
        Action twice = () => e.ConsumeClaim(claim); twice.Should().Throw<LedgerException>();
    }

    [Fact]
    public void Full_billing_claims_protect_cumulative_tolerance_on_authorized_amount()
    {
        var e = Po(); var first = e.ClaimBilling(Guid.NewGuid(), 1, Money.Of(100000));
        var stamp = e.ChangeStamp;
        Action tooMuch = () => e.ClaimBilling(Guid.NewGuid(), 1, Money.Of(5000.01m));
        tooMuch.Should().Throw<LedgerException>(); e.ChangeStamp.Should().Be(stamp);
        var second = e.ClaimBilling(Guid.NewGuid(), 1, Money.Of(5000));
        e.ConsumeBillingClaim(first); e.ConsumeBillingClaim(second);
        e.AlreadyPostedAgainstPo.Should().Be(Money.Of(105000));
        Action further = () => e.ClaimBilling(Guid.NewGuid(), 1, Money.Of(.01m));
        further.Should().Throw<LedgerException>();
    }

    [Fact]
    public void Liquidation_and_remainder_release_cannot_steal_held_claims()
    {
        var e = Po(); var claim = e.Claim(Guid.NewGuid(), 1, Money.Of(96000)); var stamp = e.ChangeStamp;
        Action liquidate = () => e.Liquidate(Money.Of(1), "OTHER");
        Action release = () => e.ReleaseRemainder("cancel");
        liquidate.Should().Throw<LedgerException>(); release.Should().Throw<LedgerException>();
        e.ChangeStamp.Should().Be(stamp); e.ReleaseClaim(claim); e.ReleaseRemainder("cancel");
        e.Status.Should().Be(EncumbranceStatus.Closed);
    }

    [Fact]
    public void Released_claims_free_capacity_and_reject_consumption()
    {
        var e = Po(); var id = Guid.NewGuid(); var claim = e.Claim(id, 1, Money.Of(50));
        var billing = e.ClaimBilling(id, 1, Money.Of(100000));
        e.ReleaseClaim(claim, id, 1); e.ReleaseBillingClaim(billing, id, 1);
        e.ClaimableForInvoice(Guid.NewGuid(), 1).Should().Be(Money.Of(96000));
        e.ClaimBilling(Guid.NewGuid(), 1, Money.Of(105000));
        Action consume = () => e.ConsumeBillingClaim(billing); consume.Should().Throw<LedgerException>();
        e.ChangeStamp.Should().Be(5);
    }

    [Fact]
    public void Opening_balance_is_a_snapshot_and_can_only_initialize_a_pristine_matching_budget()
    {
        var b = Budget(); var opening = new OpeningBalance(Account, new FiscalYear(2026), new DateOnly(2026, 6, 15), Money.Of(100000), Money.Of(160000), "seed");
        b.ApplyOpeningBalance(opening);
        b.Actuals.Should().Be(opening.InitialActuals); b.Encumbered.Should().Be(opening.InitialEncumbered);
        Action again = () => b.ApplyOpeningBalance(opening); again.Should().Throw<LedgerException>();
        b.Actuals.Should().Be(Money.Of(100000));
    }
}
