namespace GovErp.Application.Web.Ledger.Contracts;

/// <summary>Фильтры журнала; null — без ограничения. Fund — код фонда хотя бы одной строки, Source — подстрока SourceRef (без учёта регистра).</summary>
public sealed record JournalFilter(string? Fund = null, int? PeriodYear = null, int? PeriodMonth = null, string? Source = null)
{
    public static readonly JournalFilter None = new();
}
