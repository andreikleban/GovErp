namespace GovErp.Application.Web.Validation;

public sealed class PostingOptions
{
    public string AccountsPayableObject { get; set; } = "2100";
    public string ReserveForEncumbrancesObject { get; set; } = "2900";
    public string EncumbrancesObject { get; set; } = "5900";
    public string BalanceSheetDepartment { get; set; } = "0000";

    public Domain.Validation.ValueObjects.PostingAccounts ToPostingAccounts() =>
        new(new ObjectCode(AccountsPayableObject), new ObjectCode(ReserveForEncumbrancesObject),
            new ObjectCode(EncumbrancesObject), new DepartmentCode(BalanceSheetDepartment));
}
