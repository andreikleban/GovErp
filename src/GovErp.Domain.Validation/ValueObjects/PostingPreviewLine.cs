namespace GovErp.Domain.Validation.ValueObjects;

public sealed record PostingPreviewLine(AccountCode Account, string Family, Money Debit, Money Credit, string Description);
