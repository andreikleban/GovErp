namespace GovErp.Application.Web.Invoices.Contracts;

/// <summary>
/// Фильтры списка инвойсов; null — без ограничения. Status — имя статуса документа (Draft, Submitted, ...),
/// Fund — код фонда хотя бы одной строки, HasHolds — есть ли в последней оценке неснятые Soft/Hard Stop.
/// </summary>
public sealed record InvoiceListFilter(string? Status = null, string? Fund = null, bool? HasHolds = null)
{
    public static readonly InvoiceListFilter None = new();
}
