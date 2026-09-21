using GovErp.Domain.ChartOfAccounts.Entities;
using GovErp.Domain.ChartOfAccounts.Exceptions;

namespace GovErp.Domain.ChartOfAccounts.Tests;

public class AccountCombinationTests
{
    private static readonly UserId Clerk = UserId.New();
    private static readonly UserId Controller = UserId.New();
    private static readonly DateTimeOffset Now = new(2026, 9, 21, 10, 0, 0, TimeSpan.Zero);
    private static readonly AccountCode Code = AccountCode.Parse("701-6000-53100-G-COPS-26");

    private static AccountCombination Pending() =>
        AccountCombination.Request(Code, new DateOnly(2025, 7, 1), Clerk, Now, CombinationSource.Manual);

    [Fact]
    public void Requested_combination_is_pending_and_not_active()
    {
        var c = Pending();
        c.Status.Should().Be(CombinationStatus.Pending);
        c.IsActiveOn(new DateOnly(2026, 9, 21)).Should().BeFalse();
    }

    [Fact]
    public void Approve_activates_and_records_approver()
    {
        var c = Pending();
        c.Approve(Controller, Now);
        c.Status.Should().Be(CombinationStatus.Active);
        c.ApprovedBy.Should().Be(Controller);
        c.ApprovedAt.Should().Be(Now);
        c.IsActiveOn(new DateOnly(2026, 9, 21)).Should().BeTrue();
    }

    [Fact]
    public void Approve_twice_is_rejected()
    {
        var c = Pending();
        c.Approve(Controller, Now);
        FluentActions.Invoking(() => c.Approve(Controller, Now)).Should().Throw<ChartOfAccountsException>();
    }

    [Fact]
    public void Deactivate_sets_effective_to_and_status()
    {
        var c = Pending();
        c.Approve(Controller, Now);
        c.Deactivate(new DateOnly(2026, 6, 30));
        c.Status.Should().Be(CombinationStatus.Inactive);
        c.IsActiveOn(new DateOnly(2026, 6, 30)).Should().BeTrue();
        c.IsActiveOn(new DateOnly(2026, 7, 1)).Should().BeFalse();
    }

    [Fact]
    public void Deactivate_before_effective_from_is_rejected()
    {
        var c = Pending();
        c.Approve(Controller, Now);
        FluentActions.Invoking(() => c.Deactivate(new DateOnly(2025, 6, 30)))
            .Should().Throw<ChartOfAccountsException>();
    }

    [Fact]
    public void Not_active_before_effective_from()
    {
        var c = Pending();
        c.Approve(Controller, Now);
        c.IsActiveOn(new DateOnly(2025, 6, 30)).Should().BeFalse();
    }
}
