namespace GovErp.Application.Web.Invoices.Contracts;

public sealed record InvoiceListItemVm(Guid Id, string Reference, string VendorName, decimal Total, string Status, string? LastOverall, DateOnly PostingDate);
