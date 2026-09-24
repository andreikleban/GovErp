using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;

namespace GovErp.Application.Web.Tests;

public class CommandResultTests
{
    [Fact]
    public void Only_conflict_is_retryable()
    {
        CommandResult<int>.Conflict(Problem.Of(AppErrors.DataChanged)).Retryable.Should().BeTrue();
        CommandResult<int>.Refused(1, AppErrors.InvalidInput, ("parameter", "amount")).Retryable.Should().BeFalse();
        CommandResult<int>.Accepted(1).IsAccepted.Should().BeTrue();
        CommandResult<int>.Forbidden(AppErrors.OnlyApClerkCreates).Status.Should().Be(CommandStatus.Forbidden);
        CommandResult<int>.Refused(1, AppErrors.InvoiceStatus, ("status", "Posted")).Code.Should().Be(AppErrors.InvoiceStatus);
        CommandResult<int>.Refused(1, AppErrors.InvoiceStatus, ("status", "Posted")).Reason.Should().Contain("Posted");
    }
}
