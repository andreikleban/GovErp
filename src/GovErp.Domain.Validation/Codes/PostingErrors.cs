namespace GovErp.Domain.Validation.Codes;

/// <summary>
/// Codes of the reasons an invoice cannot be posted (step 8).
/// </summary>
public static class PostingErrors
{
    public const string NotApproved = "POSTING.NOT_APPROVED";
    public const string OpenStops = "POSTING.OPEN_STOPS";
    public const string PeriodClosed = "POSTING.PERIOD_CLOSED";
    public const string RevalidationRequired = "POSTING.REVALIDATION_REQUIRED";
    public const string RouteMissing = "POSTING.ROUTE_MISSING";
    public const string ApprovalMissing = "POSTING.APPROVAL_MISSING";
    public const string PreviewMissing = "POSTING.PREVIEW_MISSING";
    public const string PreviewMismatch = "POSTING.PREVIEW_MISMATCH";
    public const string PreviewUnbalanced = "POSTING.PREVIEW_UNBALANCED";
}
