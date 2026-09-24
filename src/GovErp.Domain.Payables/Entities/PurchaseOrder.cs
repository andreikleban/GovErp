using GovErp.Domain.Payables.Exceptions;
namespace GovErp.Domain.Payables.Entities;

/// <summary>
/// A purchase order and its lines.
/// </summary>
public sealed class PurchaseOrder
{
    private readonly List<PurchaseOrderLine> _lines = [];
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Number { get; private set; }
    public Guid VendorId { get; private set; }
    public PurchaseOrderStatus Status { get; private set; } = PurchaseOrderStatus.Open;
    public IReadOnlyList<PurchaseOrderLine> Lines => _lines.AsReadOnly();
    public Money Total => Lines.Aggregate(Money.Zero, (s, l) => s + l.Amount);
    private PurchaseOrder() { Number = null!; }
    public PurchaseOrder(string number, Guid vendorId, IReadOnlyList<PurchaseOrderLine> lines)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentNullException.ThrowIfNull(lines);
        if (vendorId == Guid.Empty) throw new InvalidValueException(nameof(vendorId), PayablesErrors.VendorRequired);
        if (lines.Count == 0 || lines.Any(l => l is null) || lines.Select(l => l.LineNo).Distinct().Count() != lines.Count)
            throw new PayablesException(PayablesErrors.PoLinesInvalid, ("po", number));
        _ = lines.Aggregate(Money.Zero, (s, l) => s + l.Amount);
        Number = number; VendorId = vendorId; _lines.AddRange(lines);
    }
    public string LineRef(int lineNo) => Lines.Any(l => l.LineNo == lineNo) ? $"{Number}/{lineNo}" : throw new PayablesException(PayablesErrors.PoLineNotFound, ("po", Number), ("line", lineNo));
}
