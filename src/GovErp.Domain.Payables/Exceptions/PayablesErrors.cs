namespace GovErp.Domain.Payables.Exceptions;

/// <summary>
/// Payables refusal codes. The texts live in Messages.resx.
/// </summary>
public static class PayablesErrors
{
    public const string PoLinesInvalid = "PAYABLES.PO_LINES_INVALID";
    public const string PoLineNotFound = "PAYABLES.PO_LINE_NOT_FOUND";
    public const string PoLineNotPositive = "PAYABLES.PO_LINE_NOT_POSITIVE";
    public const string BlankReference = "PAYABLES.BLANK_REFERENCE";
    public const string DistributionNotPositive = "PAYABLES.DISTRIBUTION_NOT_POSITIVE";
    public const string DistributionNotFound = "PAYABLES.DISTRIBUTION_NOT_FOUND";
    public const string DistributionsNotEqualTotal = "PAYABLES.DISTRIBUTIONS_NOT_EQUAL_TOTAL";
    public const string StaleEvaluation = "PAYABLES.STALE_EVALUATION";
    public const string AlreadyApproved = "PAYABLES.ALREADY_APPROVED";
    public const string ApprovalsMissing = "PAYABLES.APPROVALS_MISSING";
    public const string OverrideRoleInvalid = "PAYABLES.OVERRIDE_ROLE_INVALID";
    public const string OverrideMismatch = "PAYABLES.OVERRIDE_MISMATCH";
    public const string AlreadyOverridden = "PAYABLES.ALREADY_OVERRIDDEN";
    public const string StaleContent = "PAYABLES.STALE_CONTENT";
    public const string OnlyAuthorWithdraws = "PAYABLES.ONLY_AUTHOR_WITHDRAWS";
    public const string PostingNotApproved = "PAYABLES.POSTING_NOT_APPROVED";
    public const string ApprovalNotAllowed = "PAYABLES.APPROVAL_NOT_ALLOWED";
    public const string RouteInvalid = "PAYABLES.ROUTE_INVALID";
    public const string HoldIdsInvalid = "PAYABLES.HOLD_IDS_INVALID";
    public const string HeaderInvalid = "PAYABLES.HEADER_INVALID";
    public const string DueBeforeInvoice = "PAYABLES.DUE_BEFORE_INVOICE";
    public const string EvaluationRequired = "PAYABLES.EVALUATION_REQUIRED";
    public const string ActorRequired = "PAYABLES.ACTOR_REQUIRED";
    public const string ReasonRequired = "PAYABLES.REASON_REQUIRED";
    public const string WrongStatus = "PAYABLES.WRONG_STATUS";
    public const string NoActiveCycle = "PAYABLES.NO_ACTIVE_CYCLE";
    public const string DuplicateNumber = "PAYABLES.DUPLICATE_NUMBER";
    public const string AlreadyPaid = "PAYABLES.ALREADY_PAID";
    public const string PaymentNotReady = "PAYABLES.PAYMENT_NOT_READY";
    public const string VendorIdRequired = "PAYABLES.VENDOR_ID_REQUIRED";
    public const string VendorRequired = "PAYABLES.VENDOR_REQUIRED";
    public const string VendorStatusInvalid = "PAYABLES.VENDOR_STATUS_INVALID";
}
