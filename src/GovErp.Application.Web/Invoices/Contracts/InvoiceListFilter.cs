namespace GovErp.Application.Web.Invoices.Contracts;

/// <summary>
/// Invoice list filters; null means no restriction. Status is the document status name (Draft, Submitted, ...),
/// Fund is the fund code of at least one line, HasHolds is whether the latest evaluation has open Soft/Hard Stops.
/// </summary>
public sealed record InvoiceListFilter(string? Status = null, string? Fund = null, bool? HasHolds = null)
{
    public static readonly InvoiceListFilter None = new();
}
