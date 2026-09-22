namespace GovErp.Infrastructure.Common;

/// <summary>Секция "Clock": бизнес-дата демо (spec §5, правило 9).</summary>
public sealed class ClockOptions
{
    public DateOnly BusinessDate { get; set; } = new(2026, 6, 15);
}
