using GovErp.Application.Web.Common;
using Microsoft.Extensions.Options;

namespace GovErp.Infrastructure.Common;

/// <summary>Время оценок и аудита — реальное UTC; бизнес-дата — из конфигурации.</summary>
public sealed class SystemClock(IOptions<ClockOptions> options) : IClock
{
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
    public DateOnly BusinessDate => options.Value.BusinessDate;
}
