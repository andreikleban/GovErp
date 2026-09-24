namespace GovErp.Infrastructure.Common;

/// <summary>The "Clock" section: the demo business date (spec §5, rule 9).</summary>
public sealed class ClockOptions
{
    public DateOnly BusinessDate { get; set; } = new(2026, 6, 15);
}
