namespace GovErp.Application.Web.Invoices.Contracts;

/// <summary>Funds — коды фондов строк; OpenHolds — неснятые Soft/Hard Stop последней оценки.</summary>
public sealed record InvoiceListItemVm(Guid Id, string Reference, string Number, string VendorName, decimal Total, string Status, string? LastOverall,
    DateOnly PostingDate, IReadOnlyList<string> Funds, int OpenHolds);
