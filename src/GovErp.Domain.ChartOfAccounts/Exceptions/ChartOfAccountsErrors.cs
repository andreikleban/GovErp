namespace GovErp.Domain.ChartOfAccounts.Exceptions;

/// <summary>
/// Chart-of-accounts refusal codes. The texts live in Messages.resx.
/// </summary>
public static class ChartOfAccountsErrors
{
    public const string NotPending = "COA.COMBINATION_NOT_PENDING";
    public const string NotActive = "COA.COMBINATION_NOT_ACTIVE";
    public const string EffectiveToBeforeFrom = "COA.EFFECTIVE_TO_BEFORE_FROM";
    public const string NullDepartment = "COA.NULL_DEPARTMENT";
    public const string NullObject = "COA.NULL_OBJECT";
}
