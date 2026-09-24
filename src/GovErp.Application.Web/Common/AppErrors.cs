namespace GovErp.Application.Web.Common;

/// <summary>
/// Codes of the application's own refusals; the texts are in Messages.resx.
/// </summary>
public static class AppErrors
{
    public const string OnlyApClerkCreates = "APP.ONLY_AP_CLERK_CREATES";
    public const string OnlyAuthorEdits = "APP.ONLY_AUTHOR_EDITS";
    public const string OnlyAuthorSubmits = "APP.ONLY_AUTHOR_SUBMITS";
    public const string OnlyAuthorReopens = "APP.ONLY_AUTHOR_REOPENS";
    public const string OnlyPostersHoldPayment = "APP.ONLY_POSTERS_HOLD_PAYMENT";
    public const string OnlyPostersPost = "APP.ONLY_POSTERS_POST";
    public const string OnlyPostersPay = "APP.ONLY_POSTERS_PAY";
    public const string OnlyApproversApprove = "APP.ONLY_APPROVERS_APPROVE";
    public const string AuthorCannotApprove = "APP.AUTHOR_CANNOT_APPROVE";
    public const string NotPendingApprover = "APP.NOT_PENDING_APPROVER";
    public const string OnlyRouteApproverRejects = "APP.ONLY_ROUTE_APPROVER_REJECTS";
    public const string OnlyOverridersOverride = "APP.ONLY_OVERRIDERS_OVERRIDE";
    public const string CannotOverride = "APP.CANNOT_OVERRIDE";
    public const string OnlyOverridersAmend = "APP.ONLY_OVERRIDERS_AMEND";
    public const string OnlyFinanceDirectorConfiguresRules = "APP.ONLY_FINANCE_DIRECTOR_CONFIGURES_RULES";

    public const string InvoiceStatus = "APP.INVOICE_STATUS";
    public const string EvaluationNotCurrent = "APP.EVALUATION_NOT_CURRENT";
    public const string NoOpenSoftStop = "APP.NO_OPEN_SOFT_STOP";
    public const string DemoDatesMustMatch = "APP.DEMO_DATES_MUST_MATCH";
    public const string PurchaseOrderUnknown = "APP.PURCHASE_ORDER_UNKNOWN";
    public const string BlockedBy = "APP.BLOCKED_BY";
    public const string RevalidationRequired = "APP.REVALIDATION_REQUIRED";
    public const string ApprovalRequired = "APP.APPROVAL_REQUIRED";

    public const string RuleChangeReasonRequired = "APP.RULE_CHANGE_REASON_REQUIRED";
    public const string VersionStartsTooEarly = "APP.VERSION_STARTS_TOO_EARLY";
    public const string SeverityNotConfigurable = "APP.SEVERITY_NOT_CONFIGURABLE";
    public const string SeverityInvalid = "APP.SEVERITY_INVALID";
    public const string RuleHasNoParameters = "APP.RULE_HAS_NO_PARAMETERS";
    public const string RuleParametersFixed = "APP.RULE_PARAMETERS_FIXED";
    public const string ParameterUndeclared = "APP.PARAMETER_UNDECLARED";
    public const string ParameterNotNumber = "APP.PARAMETER_NOT_NUMBER";

    public const string InvoiceNotFound = "APP.INVOICE_NOT_FOUND";
    public const string VendorNotFound = "APP.VENDOR_NOT_FOUND";
    public const string BudgetLineNotFound = "APP.BUDGET_LINE_NOT_FOUND";
    public const string EvaluationNotFound = "APP.EVALUATION_NOT_FOUND";
    public const string PeriodNotFound = "APP.PERIOD_NOT_FOUND";
    public const string PurchaseOrderNotFound = "APP.PURCHASE_ORDER_NOT_FOUND";
    public const string FundNotFound = "APP.FUND_NOT_FOUND";
    public const string GrantNotFound = "APP.GRANT_NOT_FOUND";
    public const string RuleNotInCatalog = "APP.RULE_NOT_IN_CATALOG";
    public const string RuleDefinitionNotFound = "APP.RULE_DEFINITION_NOT_FOUND";
    public const string TenantNotRegistered = "APP.TENANT_NOT_REGISTERED";
    public const string UnknownInvoicePreset = "APP.UNKNOWN_INVOICE_PRESET";

    public const string DataChanged = "COMMAND.DATA_CHANGED";
    public const string AlreadyApplied = "COMMAND.ALREADY_APPLIED";
    public const string CommandOfAnotherUser = "COMMAND.OF_ANOTHER_USER";
    public const string CommandIdReused = "COMMAND.ID_REUSED";
    public const string InvalidInput = "INPUT.INVALID";
}
