using System.Globalization;
using GovErp.Application.Web.Audit;
using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Reference.Commands;
using GovErp.Application.Web.Reference.Contracts;
using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.Repositories;
using GovErp.Domain.Validation.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace GovErp.Application.Web.Reference;

/// <summary>
/// Rule configuration: a new version instead of editing an existing one. The rule logic lives in code; only the definition
/// data changes. The set is checked by the engine's resolution (conflicts, weakening a higher layer) before saving.
/// </summary>
public sealed class RuleAppService(ITenantOperationRunner runner) : IRuleAppService
{
    public Task<CommandResult<RuleVm>> CreateVersionAsync(NewRuleVersionCommand cmd, ActorContext actor, CancellationToken ct = default) =>
        runner.ExecuteAsync(actor, cmd.Envelope, "CreateRuleVersion", cmd, async (sp, token) =>
        {
            if (!actor.IsInRole(Roles.FinanceDirector))
            {
                return CommandResult<RuleVm>.Forbidden("Only the finance director changes rule configuration.");
            }

            if (string.IsNullOrWhiteSpace(cmd.Reason))
            {
                return CommandResult<RuleVm>.Refused(null, "A reason is required for a rule change.");
            }

            var repository = sp.GetRequiredService<IRuleDefinitionRepository>();
            var all = await repository.ListAsync(token);
            var source = all.SingleOrDefault(r => r.Id == cmd.SourceId) ?? throw new NotFoundException($"Rule definition {cmd.SourceId} not found.");
            if (ParameterProblem(source, cmd.Parameters) is { } problem)
            {
                return CommandResult<RuleVm>.Refused(null, problem);
            }

            if (cmd.EffectiveFrom < source.EffectiveFrom)
            {
                return CommandResult<RuleVm>.Refused(null, $"A new version cannot start before {source.EffectiveFrom:yyyy-MM-dd}, when version {source.Version} started.");
            }

            Severity? severity = source.Severity;
            if (cmd.Severity is { } requested)
            {
                if (source.Severity is null)
                {
                    return CommandResult<RuleVm>.Refused(null, $"{source.RuleId} computes its severity from the facts; it cannot be configured.");
                }

                if (!Enum.TryParse<Severity>(requested, out var parsed) || !Enum.IsDefined(parsed) || parsed == Severity.Allowed)
                {
                    return CommandResult<RuleVm>.Refused(null, $"'{requested}' is not a configurable severity (Warning, SoftStop, HardStop).");
                }

                severity = parsed;
            }

            var version = all.Where(r => r.RuleId == source.RuleId && r.Layer == source.Layer
                && r.ScopeFund == source.ScopeFund && r.ScopeGrant == source.ScopeGrant).Max(r => r.Version) + 1;
            var created = new RuleDefinition(source.RuleId, version, source.Step, source.Layer, severity, cmd.Parameters, source.OverridableBy,
                cmd.EffectiveFrom, null, source.Message, source.Resolution, enabled: true, source.ScopeFund, source.ScopeGrant);

            // The engine must resolve the set with the new version, otherwise evaluations on that date would fail with RULE_CONFIGURATION.
            // A ValidationException here becomes a runner refusal; nothing has been saved yet.
            var candidate = all.Append(created).ToList();
            RuleResolution.Resolve(candidate, cmd.EffectiveFrom, source.ScopeFund, source.ScopeGrant);

            await repository.AddAsync(created, token);
            var clock = sp.GetRequiredService<IClock>();
            var fingerprint = RuleResolution.Resolve(candidate, clock.BusinessDate).Fingerprint;
            sp.GetRequiredService<IAuditTrail>().Record(actor, "RuleVersionCreated", $"RULE:{source.RuleId}", cmd.Envelope.CommandId.ToString(),
                new
                {
                    source.RuleId, Layer = source.Layer.ToString(), FromVersion = source.Version, ToVersion = version,
                    Parameters = cmd.Parameters, Severity = severity?.ToString(), cmd.EffectiveFrom, cmd.Reason, Fingerprint = fingerprint,
                });
            return CommandResult<RuleVm>.Accepted(RuleVmMapping.ToVm(created, RuleVmMapping.CurrentIds(candidate, clock.BusinessDate).Contains(created.Id)));
        }, ct: ct);

    /// <summary>The parameter set is fixed by the rule's code: the same keys, and numeric values stay numeric.</summary>
    private static string? ParameterProblem(RuleDefinition source, IReadOnlyDictionary<string, string> parameters)
    {
        if (!source.Parameters.Keys.Order(StringComparer.Ordinal).SequenceEqual(parameters.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            return source.Parameters.Count == 0
                ? $"{source.RuleId} has no parameters."
                : $"{source.RuleId} parameters are fixed by the rule: {string.Join(", ", source.Parameters.Keys)}.";
        }

        foreach (var (key, value) in parameters)
        {
            var wasNumber = decimal.TryParse(source.Parameters[key], NumberStyles.Number, CultureInfo.InvariantCulture, out _);
            if (wasNumber && (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) || number < 0))
            {
                return $"{key} must be a non-negative number (use a dot for decimals).";
            }
        }

        return null;
    }
}
