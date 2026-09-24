using System.Globalization;
using GovErp.Application.Web.Audit;
using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Explanation;
using GovErp.Application.Web.Reference.Commands;
using GovErp.Application.Web.Reference.Contracts;
using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.Repositories;
using GovErp.Domain.Validation.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace GovErp.Application.Web.Reference;

/// <summary>
/// Validation rules: versions in force, written descriptions, and configuration. A change is a new version instead of an edit;
/// the rule logic lives in code, only the definition data changes. The set is checked by the engine's resolution
/// (conflicts, weakening a higher layer) before saving.
/// </summary>
public sealed class RuleAppService(ITenantOperationRunner runner, IRuleExplanationGenerator ruleExplanations) : IRuleAppService
{
    public Task<RuleSetVm> GetRulesAsync(ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync(actor, async (sp, token) =>
        {
            var all = await sp.GetRequiredService<IRuleDefinitionRepository>().ListAsync(token);
            var businessDate = sp.GetRequiredService<IClock>().BusinessDate;
            var current = RuleVmMapping.CurrentIds(all, businessDate);
            var rules = all
                .OrderBy(r => r.Step).ThenBy(r => r.RuleId, StringComparer.Ordinal).ThenBy(r => r.Layer).ThenBy(r => r.Version)
                .ThenBy(r => r.ScopeFund, StringComparer.Ordinal).ThenBy(r => r.ScopeGrant, StringComparer.Ordinal)
                .Select(r => RuleVmMapping.ToVm(r, current.Contains(r.Id)))
                .ToList();
            return new RuleSetVm(rules, RuleResolver.Default.Resolve(all, businessDate).Fingerprint, RuleResolver.EngineVersion);
        }, ct);

    public Task<RuleDetailVm> GetRuleDetailAsync(string ruleId, ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync(actor, async (sp, token) =>
        {
            var description = RuleDescriptions.Find(ruleId) ?? throw new NotFoundException(AppErrors.RuleNotInCatalog, ("rule", ruleId));
            var all = await sp.GetRequiredService<IRuleDefinitionRepository>().ListAsync(token);
            var current = RuleVmMapping.CurrentIds(all, sp.GetRequiredService<IClock>().BusinessDate);
            var versions = all.Where(r => r.RuleId == ruleId)
                .OrderBy(r => r.Layer).ThenBy(r => r.ScopeFund, StringComparer.Ordinal).ThenBy(r => r.ScopeGrant, StringComparer.Ordinal)
                .ThenByDescending(r => r.Version)
                .Select(r => RuleVmMapping.ToVm(r, current.Contains(r.Id)))
                .ToList();
            // The current version of the general set; scoped versions are visible in the history.
            var applied = versions.FirstOrDefault(v => v.IsCurrent && v.ScopeFund is null && v.ScopeGrant is null)
                ?? versions.FirstOrDefault(v => v.IsCurrent);
            return new RuleDetailVm(description, applied, versions);
        }, ct);

    /// <summary>
    /// The LLM is called outside a transaction and nothing is stored: a rule explanation is help text, not a record of a decision.
    /// </summary>
    public async Task<ExplanationResult> ExplainRuleAsync(string ruleId, ExplanationAudience audience, ActorContext actor, CancellationToken ct = default)
    {
        var detail = await GetRuleDetailAsync(ruleId, actor, ct);
        return await ruleExplanations.ExplainAsync(detail.Description, detail.Current, audience, ct);
    }

    public Task<CommandResult<RuleVm>> CreateVersionAsync(NewRuleVersionCommand cmd, ActorContext actor, CancellationToken ct = default) =>
        runner.ExecuteAsync(actor, cmd.Envelope, "CreateRuleVersion", cmd, async (sp, token) =>
        {
            if (!actor.IsInRole(Roles.FinanceDirector))
            {
                return CommandResult<RuleVm>.Forbidden(AppErrors.OnlyFinanceDirectorConfiguresRules);
            }

            if (string.IsNullOrWhiteSpace(cmd.Reason))
            {
                return CommandResult<RuleVm>.Refused(null, AppErrors.RuleChangeReasonRequired);
            }

            var repository = sp.GetRequiredService<IRuleDefinitionRepository>();
            var all = await repository.ListAsync(token);
            var source = all.SingleOrDefault(r => r.Id == cmd.SourceId) ?? throw new NotFoundException(AppErrors.RuleDefinitionNotFound, ("id", cmd.SourceId));
            if (ParameterProblem(source, cmd.Parameters) is { } problem)
            {
                return CommandResult<RuleVm>.Refused(null, problem);
            }

            if (cmd.EffectiveFrom < source.EffectiveFrom)
            {
                return CommandResult<RuleVm>.Refused(null, AppErrors.VersionStartsTooEarly, ("from", source.EffectiveFrom), ("version", source.Version));
            }

            Severity? severity = source.Severity;
            if (cmd.Severity is { } requested)
            {
                if (source.Severity is null)
                {
                    return CommandResult<RuleVm>.Refused(null, AppErrors.SeverityNotConfigurable, ("rule", source.RuleId));
                }

                if (!Enum.TryParse<Severity>(requested, out var parsed) || !Enum.IsDefined(parsed) || parsed == Severity.Allowed)
                {
                    return CommandResult<RuleVm>.Refused(null, AppErrors.SeverityInvalid, ("severity", requested));
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
            RuleResolver.Default.Resolve(candidate, cmd.EffectiveFrom, source.ScopeFund, source.ScopeGrant);

            await repository.AddAsync(created, token);
            var clock = sp.GetRequiredService<IClock>();
            var fingerprint = RuleResolver.Default.Resolve(candidate, clock.BusinessDate).Fingerprint;
            sp.GetRequiredService<IAuditTrail>().Record(actor, "RuleVersionCreated", $"RULE:{source.RuleId}", cmd.Envelope.CommandId.ToString(),
                new
                {
                    source.RuleId, Layer = source.Layer.ToString(), FromVersion = source.Version, ToVersion = version,
                    Parameters = cmd.Parameters, Severity = severity?.ToString(), cmd.EffectiveFrom, cmd.Reason, Fingerprint = fingerprint,
                });
            return CommandResult<RuleVm>.Accepted(RuleVmMapping.ToVm(created, RuleVmMapping.CurrentIds(candidate, clock.BusinessDate).Contains(created.Id)));
        }, ct: ct);

    /// <summary>
    /// The parameter set is fixed by the rule's code: the same keys, each value valid for the rule's ParameterSpec.
    /// </summary>
    private static Problem? ParameterProblem(RuleDefinition source, IReadOnlyDictionary<string, string> parameters)
    {
        if (!source.Parameters.Keys.Order(StringComparer.Ordinal).SequenceEqual(parameters.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            return source.Parameters.Count == 0
                ? Problem.Of(AppErrors.RuleHasNoParameters, ("rule", source.RuleId))
                : Problem.Of(AppErrors.RuleParametersFixed, ("rule", source.RuleId), ("parameters", string.Join(", ", source.Parameters.Keys)));
        }

        foreach (var (key, value) in parameters)
        {
            if (RuleCatalog.Default.ParameterOf(source.RuleId, key) is not { } spec)
            {
                if (value != source.Parameters[key]) return Problem.Of(AppErrors.ParameterUndeclared, ("parameter", key));
                continue;
            }

            if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
            {
                return Problem.Of(AppErrors.ParameterNotNumber, ("parameter", key));
            }

            if (!spec.Accepts(number))
            {
                return Problem.Of(spec.InvalidValueCode, ("rule", source.RuleId), ("parameter", key), ("value", value));
            }
        }

        return null;
    }
}
