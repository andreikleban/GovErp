namespace GovErp.Domain.Ledger.Entities;

public sealed class PoBillingClaim
{
    public Guid Id { get; } = Guid.NewGuid();
    public Guid InvoiceId { get; }
    public int ContentVersion { get; }
    public Money Amount { get; }
    public ClaimStatus Status { get; private set; } = ClaimStatus.Held;
    internal PoBillingClaim(Guid invoiceId, int contentVersion, Money amount)
    { InvoiceId = invoiceId; ContentVersion = contentVersion; Amount = amount; }
    internal void Complete(ClaimStatus status) => Status = status;
}
