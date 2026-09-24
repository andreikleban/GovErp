namespace GovErp.Application.Web.Invoices.Contracts;

/// <summary>Funds: fund codes of the lines; OpenHolds: open Soft/Hard Stops of the latest evaluation.</summary>
public sealed record InvoiceListItemVm(Guid Id, string Reference, string Number, string VendorName, decimal Total, string Status, string? LastOverall,
    DateOnly PostingDate, IReadOnlyList<string> Funds, int OpenHolds);
