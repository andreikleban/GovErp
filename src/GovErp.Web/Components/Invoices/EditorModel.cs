using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Invoices.Commands;
using GovErp.Application.Web.Invoices.Contracts;

namespace GovErp.Web.Components.Invoices;

/// <summary>The invoice card form: header and lines. For a new document it lives only in memory until Save.</summary>
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
    public bool HasPo => !string.IsNullOrWhiteSpace(PoRef);

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

    /// <summary>An empty in-memory draft: it reaches the database only on Save (CreateDraftAsync).</summary>
    public static EditorModel Blank(Guid vendorId, DateOnly documentDate) => new()
    {
        VendorId = vendorId,
        DocumentDate = documentDate,
        DueDate = documentDate.AddDays(30),
        Lines = [LineModel.Default()],
    };

    /// <summary>Whether the content matches another form: unsaved edits block Submit and Check Funds.</summary>
    public bool SameContent(EditorModel other) =>
        (Number.Trim(), VendorId, DocumentDate, DueDate, Total, HasPo ? PoRef!.Trim() : null) ==
        (other.Number.Trim(), other.VendorId, other.DocumentDate, other.DueDate, other.Total, other.HasPo ? other.PoRef!.Trim() : null)
        && Lines.Select(l => (l.Account, l.Amount, HasPo ? l.PoLineNo : null))
            .SequenceEqual(other.Lines.Select(l => (l.Account, l.Amount, other.HasPo ? l.PoLineNo : null)));

    public CreateInvoiceCommand ToCreate(CommandEnvelope envelope) =>
        new(envelope, Number, VendorId, DocumentDate, DocumentDate, DocumentDate, DueDate, Total, HasPo ? PoRef : null, Distributions());

    public UpdateInvoiceCommand ToUpdate(CommandEnvelope envelope, Guid id) =>
        new(envelope, id, Number, VendorId, DocumentDate, DocumentDate, DocumentDate, DueDate, Total, HasPo ? PoRef : null, Distributions());

    /// <summary>Without a PO the order line number is not sent: the column is hidden, and an earlier choice must not reach the server.</summary>
    private List<DistributionCommand> Distributions() =>
        Lines.Select(l => new DistributionCommand(l.Account, l.Amount, HasPo ? l.PoLineNo : null)).ToList();
}
