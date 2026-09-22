namespace GovErp.Application.Web.Common;

public interface IClock
{
    DateTimeOffset Now { get; }

    /// <summary>Бизнес-дата демо (2026-06-15) — из конфигурации, не из системных часов (spec §5, правило 9).</summary>
    DateOnly BusinessDate { get; }
}
