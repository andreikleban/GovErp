namespace GovErp.Application.Web.Common;

/// <summary>
/// Clock of an operation: the timestamp written and the business date.
/// </summary>
public interface IClock
{
    DateTimeOffset Now { get; }

    /// <summary>
    /// Demo business date (2026-06-15), taken from configuration rather than the system clock (spec §5, rule 9).
    /// </summary>
    DateOnly BusinessDate { get; }
}
