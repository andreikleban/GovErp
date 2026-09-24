using GovErp.Application.Web.Commands;

namespace GovErp.Application.Web.Reference.Commands;

/// <summary>
/// A new rule version based on an existing one (SourceId): same code, step, layer and scope; only parameters,
/// severity and effective date change. Severity = null keeps the severity of the source version.
/// </summary>
public sealed record NewRuleVersionCommand(CommandEnvelope Envelope, Guid SourceId, IReadOnlyDictionary<string, string> Parameters,
    string? Severity, DateOnly EffectiveFrom, string Reason);
