using GovErp.Domain.ChartOfAccounts.Entities;

namespace GovErp.Domain.ChartOfAccounts.Tests;

public class FundTests
{
    private static Fund StreetFund() => new(
        new FundCode("202"), "Street Fund", FundType.Governmental, AccountingBasis.ModifiedAccrual,
        BudgetControlMode.Hard, GrantPolicy.Forbidden,
        allowedDepartments: [new DepartmentCode("4000")],
        allowedObjects: [new ObjectCode("53100"), new ObjectCode("54000"), new ObjectCode("55000")]);

    private static Fund GeneralFund() => new(
        new FundCode("101"), "General Fund", FundType.Governmental, AccountingBasis.ModifiedAccrual,
        BudgetControlMode.Soft, GrantPolicy.Forbidden, allowedDepartments: [], allowedObjects: []);

    [Fact]
    public void Restricted_fund_rejects_other_department() =>
        StreetFund().Check(new DepartmentCode("3000"), new ObjectCode("53100"))
            .Should().Be(FundRestrictionCheck.DepartmentNotAllowed);

    [Fact]
    public void Restricted_fund_rejects_other_object() =>
        StreetFund().Check(new DepartmentCode("4000"), new ObjectCode("51000"))
            .Should().Be(FundRestrictionCheck.ObjectNotAllowed);

    [Fact]
    public void Restricted_fund_allows_listed_pair() =>
        StreetFund().Check(new DepartmentCode("4000"), new ObjectCode("54000"))
            .Should().Be(FundRestrictionCheck.Allowed);

    [Fact]
    public void Empty_lists_mean_unrestricted() =>
        GeneralFund().Check(new DepartmentCode("9999"), new ObjectCode("99999"))
            .Should().Be(FundRestrictionCheck.Allowed);

    [Fact]
    public void Name_is_required() =>
        FluentActions.Invoking(() => new Fund(new FundCode("101"), " ", FundType.Governmental,
                AccountingBasis.ModifiedAccrual, BudgetControlMode.Soft, GrantPolicy.Forbidden, [], []))
            .Should().Throw<ArgumentException>();
}
