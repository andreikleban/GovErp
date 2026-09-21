namespace GovErp.Domain.Payables.Entities;

public sealed class Vendor
{
    public Guid Id { get; }
    public string Code { get; }
    public string Name { get; }
    public VendorStatus Status { get; }
    public bool SamRegistered { get; }
    public Vendor(Guid id, string code, string name, VendorStatus status, bool samRegistered)
    {
        if (id == Guid.Empty) throw new ArgumentException("Vendor ID required.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        Id = id; Code = code; Name = name; Status = status; SamRegistered = samRegistered;
    }
}
