using GovErp.Application.Web.Commands;

namespace GovErp.Application.Web.Reference.Commands;

/// <summary>
/// Новая версия правила на основе существующей (SourceId): те же код, шаг, слой и scope; меняются только параметры,
/// серьёзность и дата начала действия. Severity = null оставляет серьёзность исходной версии.
/// </summary>
public sealed record NewRuleVersionCommand(CommandEnvelope Envelope, Guid SourceId, IReadOnlyDictionary<string, string> Parameters,
    string? Severity, DateOnly EffectiveFrom, string Reason);
