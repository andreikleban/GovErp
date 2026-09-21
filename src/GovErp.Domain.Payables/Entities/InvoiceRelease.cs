namespace GovErp.Domain.Payables.Entities;

public sealed record InvoiceRelease(Guid InvoiceId, int ContentVersion, IReadOnlyList<Guid> ReservationRefs,
    IReadOnlyList<Guid> EncumbranceClaimRefs, IReadOnlyList<Guid> PoBillingClaimRefs);
