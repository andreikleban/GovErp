namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>
/// One line of a future posting.
/// </summary>
public sealed record PostingPreviewLine(AccountCode Account, string Family, Money Debit, Money Credit, string Description);
