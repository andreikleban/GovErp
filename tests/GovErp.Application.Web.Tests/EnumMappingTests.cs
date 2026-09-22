using Coa = GovErp.Domain.ChartOfAccounts.Entities;
using Led = GovErp.Domain.Ledger.Entities;
using Pay = GovErp.Domain.Payables.Entities;
using Val = GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Application.Web.Tests;

public class EnumMappingTests
{
    [Theory]
    [InlineData(typeof(Coa.FundRestrictionCheck), typeof(Val.FundRestriction))]
    [InlineData(typeof(Coa.GrantEligibility), typeof(Val.GrantEligibilityResult))]
    [InlineData(typeof(Coa.GrantPolicy), typeof(Val.GrantRule))]
    [InlineData(typeof(Coa.FundType), typeof(Val.FundKind))]
    [InlineData(typeof(Coa.BudgetControlMode), typeof(Val.BudgetControl))]
    [InlineData(typeof(Coa.BudgetControlMode), typeof(Led.BudgetControlMode))]
    [InlineData(typeof(Pay.ApproverRole), typeof(Val.ApproverRole))]
    public void Context_enums_mapped_by_name_have_identical_names(Type a, Type b) =>
        Enum.GetNames(a).Should().BeEquivalentTo(Enum.GetNames(b));
}
