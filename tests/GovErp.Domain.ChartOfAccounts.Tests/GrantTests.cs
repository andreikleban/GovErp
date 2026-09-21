using GovErp.Domain.ChartOfAccounts.Entities;

namespace GovErp.Domain.ChartOfAccounts.Tests;

public class GrantTests
{
    private static Grant Cops(GrantStatus status = GrantStatus.Active) => new(
        new GrantCode("G-COPS-26"), "COPS Hiring Program", "US DOJ", isFederal: true,
        new DatePeriod(new DateOnly(2025, 7, 1), new DateOnly(2027, 6, 30)),
        allowedDepartments: [new DepartmentCode("3000"), new DepartmentCode("6000")],
        allowableObjects: [new ObjectCode("53100"), new ObjectCode("54000")],
        status);

    private static readonly DateOnly InPeriod = new(2026, 9, 15);

    [Fact]
    public void Eligible_when_all_match() =>
        Cops().CheckEligibility(InPeriod, new DepartmentCode("6000"), new ObjectCode("53100"))
            .Should().Be(GrantEligibility.Eligible);

    [Fact]
    public void Closed_grant_is_not_active() =>
        Cops(GrantStatus.Closed).CheckEligibility(InPeriod, new DepartmentCode("6000"), new ObjectCode("53100"))
            .Should().Be(GrantEligibility.GrantNotActive);

    [Fact]
    public void Date_outside_period() =>
        Cops().CheckEligibility(new DateOnly(2025, 6, 30), new DepartmentCode("6000"), new ObjectCode("53100"))
            .Should().Be(GrantEligibility.OutsidePeriod);

    [Fact]
    public void Department_not_allowed() =>
        Cops().CheckEligibility(InPeriod, new DepartmentCode("4000"), new ObjectCode("53100"))
            .Should().Be(GrantEligibility.DepartmentNotAllowed);

    [Fact]
    public void Object_not_allowable() =>
        Cops().CheckEligibility(InPeriod, new DepartmentCode("6000"), new ObjectCode("55000"))
            .Should().Be(GrantEligibility.ObjectNotAllowed);
}
