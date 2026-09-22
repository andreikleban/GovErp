namespace GovErp.Domain.Validation.ValueObjects;

public sealed record PostingAccounts(ObjectCode AccountsPayable, ObjectCode ReserveForEncumbrances,
    ObjectCode Encumbrances, DepartmentCode BalanceSheetDepartment);
