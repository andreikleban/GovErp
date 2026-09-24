using GovErp.Application.Web.Common;
using Microsoft.Extensions.Options;

namespace GovErp.Infrastructure.Common;

/// <summary>Evaluation and audit time is real UTC; the business date comes from configuration.</summary>
public sealed class SystemClock(IOptions<ClockOptions> options) : IClock
{
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
    public DateOnly BusinessDate => options.Value.BusinessDate;
}
