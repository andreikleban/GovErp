namespace GovErp.Domain.Ledger.Entities;

public sealed class EncumbranceClaim
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid InvoiceId { get; private set; }
    public int ContentVersion { get; private set; }
    public Money Amount { get; private set; }
    public ClaimStatus Status { get; private set; } = ClaimStatus.Held;
    private EncumbranceClaim() { }
    internal EncumbranceClaim(Guid invoiceId, int contentVersion, Money amount)
    { InvoiceId = invoiceId; ContentVersion = contentVersion; Amount = amount; }
    internal void Complete(ClaimStatus status) => Status = status;
}
