using GovErp.Domain.ChartOfAccounts.Entities;
using GovErp.Domain.ChartOfAccounts.Exceptions;

namespace GovErp.Domain.ChartOfAccounts.Tests;

public class InvariantTests
{
    private static readonly DateOnly Start = new(2026, 1, 1);
    private static readonly DateOnly End = new(2026, 12, 31);
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DepartmentCode Dept = new("4000");
    private static readonly ObjectCode Object = new("53100");
    private static AccountCombination Pending() => AccountCombination.Request(
        AccountCode.Parse("202-4000-53100"), Start, UserId.New(), Now, CombinationSource.Manual);
    private static Fund Fund(IReadOnlyList<DepartmentCode> departments, IReadOnlyList<ObjectCode> objects) =>
        new(new FundCode("202"), "Street", FundType.Governmental, AccountingBasis.ModifiedAccrual,
            BudgetControlMode.Hard, GrantPolicy.Forbidden, departments, objects);
    private static Grant Grant(IReadOnlyList<DepartmentCode> departments, IReadOnlyList<ObjectCode> objects,
        GrantStatus status = GrantStatus.Active) => new(new GrantCode("G-COPS-26"), "COPS", "DOJ", true,
            new DatePeriod(Start, End), departments, objects, status);

    [Fact]
    public void Pending_cannot_be_deactivated_into_an_approved_historical_account()
    {
        var account = Pending();
        FluentActions.Invoking(() => account.Deactivate(End)).Should().Throw<ChartOfAccountsException>();
        account.Status.Should().Be(CombinationStatus.Pending);
        account.IsActiveOn(Start).Should().BeFalse();
        account.EffectiveTo.Should().BeNull();
    }

    [Fact]
    public void Effective_dates_are_inclusive_and_inactive_cannot_transition_again()
    {
        var account = Pending();
        account.Approve(UserId.New(), Now);
        account.Deactivate(Start);
        account.IsActiveOn(Start).Should().BeTrue();
        account.IsActiveOn(Start.AddDays(-1)).Should().BeFalse();
        account.IsActiveOn(Start.AddDays(1)).Should().BeFalse();
        FluentActions.Invoking(() => account.Deactivate(End)).Should().Throw<ChartOfAccountsException>();
        FluentActions.Invoking(() => account.Approve(UserId.New(), Now)).Should().Throw<ChartOfAccountsException>();
        account.EffectiveTo.Should().Be(Start);
    }

    [Fact]
    public void Default_requester_and_approver_are_rejected_without_mutation()
    {
        FluentActions.Invoking(() => AccountCombination.Request(AccountCode.Parse("202-4000-53100"),
            Start, default, Now, CombinationSource.Manual)).Should().Throw<ArgumentException>();
        var account = Pending();
        FluentActions.Invoking(() => account.Approve(default, Now)).Should().Throw<ArgumentException>();
        account.Status.Should().Be(CombinationStatus.Pending);
        account.ApprovedBy.Should().BeNull();
        account.ApprovedAt.Should().BeNull();
    }

    [Fact]
    public void Required_references_and_collection_elements_cannot_be_null()
    {
        FluentActions.Invoking(() => new Department(null!, "Roads")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new ObjectCodeDefinition(null!, "Roads", ObjectCategory.Expenditure)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => Fund(null!, [])).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => Fund([], null!)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => Fund([null!], [])).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => Fund([], [null!])).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => Grant(null!, [])).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => Grant([], null!)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => Grant([null!], [])).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => Grant([], [null!])).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => AccountCombination.Request(null!, Start, UserId.New(), Now,
            CombinationSource.Manual)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Restriction_collections_are_defensive_and_read_only()
    {
        var departments = new List<DepartmentCode> { Dept };
        var objects = new List<ObjectCode> { Object };
        var fund = Fund(departments, objects);
        var grant = Grant(departments, objects);
        departments.Clear();
        objects.Clear();
        fund.Check(new DepartmentCode("3000"), Object).Should().Be(FundRestrictionCheck.DepartmentNotAllowed);
        grant.CheckEligibility(Start, Dept, new ObjectCode("55000")).Should().Be(GrantEligibility.ObjectNotAllowed);
        AssertReadOnly(fund.AllowedDepartments, Dept);
        AssertReadOnly(fund.AllowedObjects, Object);
        AssertReadOnly(grant.AllowedDepartments, Dept);
        AssertReadOnly(grant.AllowableObjects, Object);
    }

    private static void AssertReadOnly<T>(IReadOnlyList<T> items, T item)
    {
        if (items is IList<T> list)
        {
            FluentActions.Invoking(() => list.Add(item)).Should().Throw<NotSupportedException>();
            FluentActions.Invoking(() => list[0] = item).Should().Throw<NotSupportedException>();
        }
        items.Should().ContainSingle();
    }

    [Fact]
    public void Grant_period_is_inclusive_and_empty_restrictions_allow_any_valid_codes()
    {
        var grant = Grant([], []);
        grant.CheckEligibility(Start, Dept, Object).Should().Be(GrantEligibility.Eligible);
        grant.CheckEligibility(End, Dept, Object).Should().Be(GrantEligibility.Eligible);
        grant.CheckEligibility(End.AddDays(1), Dept, Object).Should().Be(GrantEligibility.OutsidePeriod);
        Grant([], [], GrantStatus.Suspended).CheckEligibility(Start, Dept, Object).Should().Be(GrantEligibility.GrantNotActive);
    }

    [Fact]
    public void Checks_reject_missing_segments_even_without_restrictions()
    {
        FluentActions.Invoking(() => Fund([], []).Check(null!, Object)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => Fund([], []).Check(Dept, null!)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => Grant([], []).CheckEligibility(Start, null!, Object)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => Grant([], []).CheckEligibility(Start, Dept, null!)).Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Reference_names_and_sponsor_are_required(string? name)
    {
        FluentActions.Invoking(() => new Department(Dept, name!)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new ObjectCodeDefinition(Object, name!, ObjectCategory.Expenditure)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new Grant(new GrantCode("G-COPS-26"), name!, "DOJ", true,
            new DatePeriod(Start, End), [], [])).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new Grant(new GrantCode("G-COPS-26"), "COPS", name!, true,
            new DatePeriod(Start, End), [], [])).Should().Throw<ArgumentException>();
    }
}
