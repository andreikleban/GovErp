using GovErp.Domain.Payables.Exceptions;

namespace GovErp.Domain.Payables.Entities;

/// <summary>
/// A vendor: code, status and SAM registration.
/// </summary>
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
        if (id == Guid.Empty) throw new InvalidValueException(nameof(id), PayablesErrors.VendorIdRequired);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!Enum.IsDefined(status)) throw new InvalidValueException(nameof(status), PayablesErrors.VendorStatusInvalid, ("status", status));
        Id = id; Code = code; Name = name; Status = status; SamRegistered = samRegistered;
    }
}
