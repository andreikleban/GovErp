namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>
/// Object codes used for AP, reserve and encumbrance preview lines.
/// </summary>
public sealed record PostingAccounts(ObjectCode AccountsPayable, ObjectCode ReserveForEncumbrances,
    ObjectCode Encumbrances, DepartmentCode BalanceSheetDepartment);
