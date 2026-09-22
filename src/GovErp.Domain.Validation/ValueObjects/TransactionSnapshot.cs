namespace GovErp.Domain.Validation.ValueObjects;

public sealed record TransactionSnapshot(
    string TransactionRef, int TransactionVersion, string TransactionType, DateOnly Date, Money Total,
    VendorSnapshot Vendor, bool IsPoBacked, bool IsDuplicate, UserId CreatedBy,
    Guid ApprovalCycleId = default, string RuleFingerprint = "", string Status = "Draft",
    DateOnly? ServiceDate = null, DateOnly? PostingDate = null, DateOnly? DueDate = null, bool PaymentHold = false)
{
    public int ContentVersion => TransactionVersion;
    public DateOnly InvoiceDate => Date;
}
