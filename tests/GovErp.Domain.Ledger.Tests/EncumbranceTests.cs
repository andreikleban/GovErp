using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Ledger.Exceptions;

namespace GovErp.Domain.Ledger.Tests;

public class EncumbranceTests
{
    private static readonly AccountCode Cops = AccountCode.Parse("701-3000-53100-G-COPS-26");

    private static Encumbrance Open() => new("PO-2026-0451/1", Cops, Money.Of(160_000m));

    [Fact]
    public void New_encumbrance_is_open_with_full_remaining()
    {
        var e = Open();
        e.Remaining.Should().Be(Money.Of(160_000m));
        e.Status.Should().Be(EncumbranceStatus.Open);
    }

    [Fact]
    public void Partial_liquidation_keeps_it_open()
    {
        var e = Open();
        e.Liquidate(Money.Of(32_000m), "INV-1");
        e.Liquidated.Should().Be(Money.Of(32_000m));
        e.Remaining.Should().Be(Money.Of(128_000m));
        e.Status.Should().Be(EncumbranceStatus.Open);
        e.Liquidations.Should().ContainSingle(l => l.SourceRef == "INV-1" && l.Amount == Money.Of(32_000m));
    }

    [Fact]
    public void Full_liquidation_closes()
    {
        var e = Open();
        e.Liquidate(Money.Of(160_000m), "INV-1");
        e.Remaining.Should().Be(Money.Zero);
        e.Status.Should().Be(EncumbranceStatus.Closed);
    }

    [Fact]
    public void Liquidating_more_than_remaining_is_rejected()
    {
        var e = Open();
        FluentActions.Invoking(() => e.Liquidate(Money.Of(160_001m), "INV-1")).Should().Throw<LedgerException>();
    }

    [Fact]
    public void Release_remainder_closes_and_records_released()
    {
        var e = Open();
        e.Liquidate(Money.Of(90_000m), "INV-1");
        e.ReleaseRemainder("final invoice");
        e.Released.Should().Be(Money.Of(70_000m));
        e.Remaining.Should().Be(Money.Zero);
        e.Status.Should().Be(EncumbranceStatus.Closed);
    }

    [Fact]
    public void Closed_encumbrance_rejects_liquidation()
    {
        var e = Open();
        e.ReleaseRemainder("cancelled");
        FluentActions.Invoking(() => e.Liquidate(Money.Of(1), "INV-9")).Should().Throw<LedgerException>();
    }
}
