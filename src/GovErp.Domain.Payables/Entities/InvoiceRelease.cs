namespace GovErp.Domain.Payables.Entities;

/// <summary>
/// What to release from an invoice on reject or withdraw.
/// </summary>
public sealed record InvoiceRelease(Guid InvoiceId, int ContentVersion, IReadOnlyList<Guid> ReservationRefs,
    IReadOnlyList<Guid> EncumbranceClaimRefs, IReadOnlyList<Guid> PoBillingClaimRefs);
