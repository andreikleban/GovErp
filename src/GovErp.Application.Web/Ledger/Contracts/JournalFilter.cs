namespace GovErp.Application.Web.Ledger.Contracts;

/// <summary>Journal filters; null means no restriction. Fund is the fund code of at least one line, Source is a case-insensitive substring of SourceRef.</summary>
public sealed record JournalFilter(string? Fund = null, int? PeriodYear = null, int? PeriodMonth = null, string? Source = null)
{
    public static readonly JournalFilter None = new();
}
