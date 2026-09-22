using GovErp.Application.Web.Common;

namespace GovErp.Application.Web.Tests;

public sealed class FixedClock(DateTimeOffset now, DateOnly businessDate) : IClock
{
    public DateTimeOffset Now => now;
    public DateOnly BusinessDate => businessDate;
}
