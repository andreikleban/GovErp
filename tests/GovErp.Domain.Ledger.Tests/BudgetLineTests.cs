using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Ledger.Exceptions;

namespace GovErp.Domain.Ledger.Tests;

public class BudgetLineTests
{
    private static readonly AccountCode Cops = AccountCode.Parse("701-6000-53100-G-COPS-26");
    private static readonly FiscalYear Fy = new(2026);
    private static readonly DateOnly Today = new(2026, 6, 15);

    /// <summary>Строка задания: 375,000 − 132,000 − 96,000 = 147,000.</summary>
    private static BudgetLine ExerciseLine(BudgetControlMode mode = BudgetControlMode.Hard)
    {
        var line = new BudgetLine(Cops, Fy, mode, adopted: Money.Of(375_000m));
        line.ApplyOpeningBalance(new OpeningBalance(Cops, Fy, new DateOnly(2026, 6, 1), Money.Of(132_000m), Money.Of(96_000m), "FY2026 opening load"));
        return line;
    }

    [Fact]
    public void Available_is_amended_minus_actuals_minus_encumbered_minus_held()
    {
        var line = ExerciseLine();
        line.Amended.Should().Be(Money.Of(375_000m));
        line.Available.Should().Be(Money.Of(147_000m));
    }

    [Fact]
    public void Hard_control_refuses_reservation_over_available()
    {
        var line = ExerciseLine();
        var result = line.Reserve(Guid.NewGuid(), 1, Money.Of(160_000m), "INV-1");
        result.IsReserved.Should().BeFalse();
        result.Shortfall.Should().Be(Money.Of(13_000m));
        result.AvailableBefore.Should().Be(Money.Of(147_000m));
        line.Held.Should().Be(Money.Zero);
        line.Reservations.Should().BeEmpty();
    }

    [Fact]
    public void Soft_control_reserves_with_overage_flag()
    {
        var line = ExerciseLine(BudgetControlMode.Soft);
        var result = line.Reserve(Guid.NewGuid(), 1, Money.Of(160_000m), "INV-1");
        result.IsReserved.Should().BeTrue();
        result.IsOverage.Should().BeTrue();
        line.Held.Should().Be(Money.Of(160_000m));
        line.Available.Should().Be(Money.Of(-13_000m));
    }

    [Fact]
    public void Reservation_reduces_available_for_the_next_one()
    {
        var line = ExerciseLine();
        line.Reserve(Guid.NewGuid(), 1, Money.Of(100_000m), "INV-A").IsReserved.Should().BeTrue();
        line.Available.Should().Be(Money.Of(47_000m));
        line.Reserve(Guid.NewGuid(), 1, Money.Of(100_000m), "INV-B").IsReserved.Should().BeFalse();
    }

    [Fact]
    public void Amend_raises_amended_and_available()
    {
        var line = ExerciseLine();
        line.Amend(Money.Of(13_000m), "BA-2026-14", Today);
        line.Amended.Should().Be(Money.Of(388_000m));
        line.Available.Should().Be(Money.Of(160_000m));
        line.Amendments.Should().ContainSingle(a => a.Reference == "BA-2026-14" && a.Amount == Money.Of(13_000m));
        line.Reserve(Guid.NewGuid(), 1, Money.Of(160_000m), "INV-1").IsReserved.Should().BeTrue();
    }

    [Fact]
    public void Commit_moves_held_to_actuals()
    {
        var line = ExerciseLine();
        var r = line.Reserve(Guid.NewGuid(), 1, Money.Of(100_000m), "INV-A");
        line.Commit(r.ReservationId!.Value);
        line.Held.Should().Be(Money.Zero);
        line.Actuals.Should().Be(Money.Of(232_000m));
        line.Available.Should().Be(Money.Of(47_000m));
        line.Reservations.Single().Status.Should().Be(ReservationStatus.Committed);
    }

    [Fact]
    public void Commit_twice_is_rejected()
    {
        var line = ExerciseLine();
        var r = line.Reserve(Guid.NewGuid(), 1, Money.Of(100_000m), "INV-A");
        line.Commit(r.ReservationId!.Value);
        FluentActions.Invoking(() => line.Commit(r.ReservationId!.Value)).Should().Throw<LedgerException>();
    }

    [Fact]
    public void Release_frees_held()
    {
        var line = ExerciseLine();
        var r = line.Reserve(Guid.NewGuid(), 1, Money.Of(100_000m), "INV-A");
        line.Release(r.ReservationId!.Value);
        line.Held.Should().Be(Money.Zero);
        line.Available.Should().Be(Money.Of(147_000m));
        line.Reservations.Single().Status.Should().Be(ReservationStatus.Released);
    }

    [Fact]
    public void Unknown_reservation_is_rejected() =>
        FluentActions.Invoking(() => ExerciseLine().Commit(Guid.NewGuid())).Should().Throw<LedgerException>();

    [Fact]
    public void Liquidation_moves_encumbered_to_actuals_and_keeps_available()
    {
        var line = ExerciseLine();
        line.RecordLiquidation(Money.Of(36_000m));
        line.Encumbered.Should().Be(Money.Of(60_000m));
        line.Actuals.Should().Be(Money.Of(168_000m));
        line.Available.Should().Be(Money.Of(147_000m));
    }

    [Fact]
    public void Liquidation_over_encumbered_is_rejected() =>
        FluentActions.Invoking(() => ExerciseLine().RecordLiquidation(Money.Of(96_001m)))
            .Should().Throw<LedgerException>();

    [Fact]
    public void Negative_or_zero_amounts_are_rejected()
    {
        var line = ExerciseLine();
        FluentActions.Invoking(() => line.Reserve(Guid.NewGuid(), 1, Money.Zero, "X")).Should().Throw<LedgerException>();
        FluentActions.Invoking(() => line.Amend(Money.Zero, "X", Today)).Should().Throw<LedgerException>();
        FluentActions.Invoking(() => line.RecordLiquidation(Money.Of(-1))).Should().Throw<LedgerException>();
    }
}
