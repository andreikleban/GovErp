using GovErp.Domain.Payables.Exceptions;
namespace GovErp.Domain.Payables.Entities;

public sealed class PurchaseOrder
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Number { get; }
    public Guid VendorId { get; }
    public PurchaseOrderStatus Status { get; } = PurchaseOrderStatus.Open;
    public IReadOnlyList<PurchaseOrderLine> Lines { get; }
    public Money Total => Lines.Aggregate(Money.Zero, (s, l) => s + l.Amount);
    public PurchaseOrder(string number, Guid vendorId, IReadOnlyList<PurchaseOrderLine> lines)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentNullException.ThrowIfNull(lines);
        if (vendorId == Guid.Empty) throw new ArgumentException("Vendor required.", nameof(vendorId));
        if (lines.Count == 0 || lines.Any(l => l is null) || lines.Select(l => l.LineNo).Distinct().Count() != lines.Count)
            throw new PayablesException("PO requires distinct, nonempty lines.");
        _ = lines.Aggregate(Money.Zero, (s, l) => s + l.Amount);
        Number = number; VendorId = vendorId; Lines = Array.AsReadOnly(lines.ToArray());
    }
    public string LineRef(int lineNo) => Lines.Any(l => l.LineNo == lineNo) ? $"{Number}/{lineNo}" : throw new PayablesException("PO line not found.");
}
