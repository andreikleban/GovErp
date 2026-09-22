namespace GovErp.Domain.Payables.Entities;

public sealed class Vendor
{
    public Guid Id { get; private set; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public VendorStatus Status { get; private set; }
    public bool SamRegistered { get; private set; }
    private Vendor() { Code = null!; Name = null!; }
    public Vendor(Guid id, string code, string name, VendorStatus status, bool samRegistered)
    {
        if (id == Guid.Empty) throw new ArgumentException("Vendor ID required.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        Id = id; Code = code; Name = name; Status = status; SamRegistered = samRegistered;
    }
}
