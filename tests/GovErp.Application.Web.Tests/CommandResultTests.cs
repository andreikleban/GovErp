using GovErp.Application.Web.Commands;

namespace GovErp.Application.Web.Tests;

public class CommandResultTests
{
    [Fact]
    public void Only_conflict_is_retryable()
    {
        CommandResult<int>.Conflict("x").Retryable.Should().BeTrue();
        CommandResult<int>.Refused(1, "x").Retryable.Should().BeFalse();
        CommandResult<int>.Accepted(1).IsAccepted.Should().BeTrue();
        CommandResult<int>.Forbidden("x").Status.Should().Be(CommandStatus.Forbidden);
    }
}
