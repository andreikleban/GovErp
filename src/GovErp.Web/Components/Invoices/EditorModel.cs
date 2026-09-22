using GovErp.Application.Web.Invoices.Commands;
using GovErp.Application.Web.Invoices.Contracts;

namespace GovErp.Web.Components.Invoices;

public sealed class EditorModel
{
    public string Number { get; set; } = "";
    public Guid VendorId { get; set; }
    public DateOnly DocumentDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal Total { get; set; }
    public string? PoRef { get; set; }
    public List<LineModel> Lines { get; set; } = [];

    public decimal LineSum => Lines.Sum(l => l.Amount);
    public decimal Difference => Total - LineSum;

    public static EditorModel From(InvoiceVm invoice) => new()
    {
        Number = invoice.Number,
        VendorId = invoice.VendorId,
        DocumentDate = invoice.InvoiceDate,
        DueDate = invoice.DueDate,
        Total = invoice.Total,
        PoRef = invoice.PoRef,
        Lines = invoice.Distributions.Select(LineModel.From).ToList(),
    };

    public UpdateInvoiceCommand ToUpdate(GovErp.Application.Web.Commands.CommandEnvelope envelope, Guid id) =>
        new(envelope, id, Number, VendorId, DocumentDate, DocumentDate, DocumentDate, DueDate, Total,
            string.IsNullOrWhiteSpace(PoRef) ? null : PoRef,
            Lines.Select(l => new DistributionCommand(l.Account, l.Amount, l.PoLineNo)).ToList());
}

public sealed class LineModel
{
    public string Fund { get; set; } = "";
    public string Department { get; set; } = "";
    public string Object { get; set; } = "";
    public string? Grant { get; set; }
    public decimal Amount { get; set; }
    public int? PoLineNo { get; set; }

    public string Account => string.IsNullOrWhiteSpace(Grant) ? $"{Fund}-{Department}-{Object}" : $"{Fund}-{Department}-{Object}-{Grant}";

    public static LineModel From(DistributionVm d)
    {
        var parts = d.Account.Split('-');
        return new LineModel
        {
            Fund = parts.ElementAtOrDefault(0) ?? "",
            Department = parts.ElementAtOrDefault(1) ?? "",
            Object = parts.ElementAtOrDefault(2) ?? "",
            Grant = parts.Length > 3 ? string.Join('-', parts.Skip(3)) : null,
            Amount = d.Amount,
            PoLineNo = d.PoLineNo,
        };
    }
}
