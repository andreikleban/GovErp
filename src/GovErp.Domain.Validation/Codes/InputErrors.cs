namespace GovErp.Domain.Validation.Codes;

/// <summary>
/// Codes of input problems, refused as VALIDATION_INPUT before any rule runs.
/// </summary>
public static class InputErrors
{
    public const string ActorRequired = "INPUT.ACTOR_REQUIRED";
    public const string NoLines = "INPUT.NO_LINES";
    public const string LineNumberNotPositive = "INPUT.LINE_NUMBER_NOT_POSITIVE";
    public const string LineNumberDuplicate = "INPUT.LINE_NUMBER_DUPLICATE";
    public const string LineAmountNotPositive = "INPUT.LINE_AMOUNT_NOT_POSITIVE";
    public const string LineAmountsTooLarge = "INPUT.LINE_AMOUNTS_TOO_LARGE";
    public const string LinesNotEqualTotal = "INPUT.LINES_NOT_EQUAL_TOTAL";
    public const string ContentVersionNotPositive = "INPUT.CONTENT_VERSION_NOT_POSITIVE";
    public const string AuthorRequired = "INPUT.AUTHOR_REQUIRED";
    public const string UnknownStatus = "INPUT.UNKNOWN_STATUS";
    public const string ApprovalCycleRequired = "INPUT.APPROVAL_CYCLE_REQUIRED";
    public const string DatesRequired = "INPUT.DATES_REQUIRED";
    public const string DatesMustMatch = "INPUT.DATES_MUST_MATCH";
    public const string BudgetYearMismatch = "INPUT.BUDGET_YEAR_MISMATCH";
    public const string VendorNotIdentified = "INPUT.VENDOR_NOT_IDENTIFIED";
    public const string CombinationFactsMissing = "INPUT.COMBINATION_FACTS_MISSING";
    public const string FundFactsMissing = "INPUT.FUND_FACTS_MISSING";
    public const string FundFactsMismatch = "INPUT.FUND_FACTS_MISMATCH";
    public const string GrantFactsMissing = "INPUT.GRANT_FACTS_MISSING";
    public const string GrantFactsMismatch = "INPUT.GRANT_FACTS_MISMATCH";
    public const string PoLineMissing = "INPUT.PO_LINE_MISSING";
}
