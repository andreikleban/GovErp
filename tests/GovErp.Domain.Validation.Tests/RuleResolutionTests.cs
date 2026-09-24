using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.Exceptions;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Tests;

public class RuleResolutionTests
{
    private static readonly DateOnly Date = new(2026, 6, 15);
    private const string Procurement = "PROCUREMENT_THRESHOLD";
    private static RuleDefinition Rule(string id = "X", RuleLayer layer = RuleLayer.Core, int version = 1,
        Severity? severity = Severity.HardStop, bool enabled = true, string? fund = null, string? grant = null,
        Dictionary<string, string>? parameters = null, ApproverRole[]? roles = null,
        DateOnly? from = null, DateOnly? to = null, string message = "message", string resolution = "resolution", ValidationStep step = ValidationStep.TransactionPurpose) =>
        new(id, version, step, layer, severity, parameters ?? [], roles ?? [],
            from ?? Date, to, message, resolution, enabled, fund, grant);

    [Fact]
    public void Resolves_latest_version_and_inclusive_date_and_scope()
    {
        var latest = Rule(version: 2, fund: "101", grant: "G", to: Date);
        var set = RuleResolver.Default.Resolve([Rule(), latest, Rule("future", from: Date.AddDays(1)),
            Rule("expired", from: Date.AddDays(-2), to: Date.AddDays(-1)), Rule("other", fund: "202")], Date, "101", "G");
        set.Rules.Should().Equal(latest);
        set.Find("X").Should().BeSameAs(latest);
        set.ForStep(ValidationStep.TransactionPurpose).Should().Equal(latest);
        RuleResolver.Default.Resolve([latest], Date).Rules.Should().BeEmpty();
    }

    [Fact]
    public void Disabled_latest_version_does_not_resurrect_old_version() =>
        RuleResolver.Default.Resolve([Rule(), Rule(version: 2, enabled: false)], Date).Rules.Should().BeEmpty();

    [Fact]
    public void Ambiguous_same_layer_version_is_rejected_even_with_different_scopes() =>
        FluentActions.Invoking(() => RuleResolver.Default.Resolve([Rule(), Rule(fund: "101")], Date, "101"))
            .Should().Throw<ValidationException>();

    [Theory]
    [InlineData(false, Severity.HardStop)]
    [InlineData(true, Severity.Warning)]
    public void Mandatory_guard_cannot_be_disabled_or_lowered(bool enabled, Severity severity) =>
        FluentActions.Invoking(() => RuleResolver.Default.Resolve([Rule(), Rule(layer: RuleLayer.Tenant, enabled: enabled, severity: severity)], Date))
            .Should().Throw<ValidationException>();

    [Theory]
    [InlineData("PROCUREMENT_THRESHOLD", "threshold", "25000", "50000")]
    [InlineData("PO_LIQUIDATION", "tolerance_pct", "0.05", "0.10")]
    public void Parameter_weakening_is_rejected_even_at_same_severity(string id, string key, string baseline, string local) =>
        FluentActions.Invoking(() => RuleResolver.Default.Resolve([
            Rule(id, parameters: new() { [key] = baseline }),
            Rule(id, layer: RuleLayer.Tenant, parameters: new() { [key] = local })], Date))
            .Should().Throw<ValidationException>();

    [Fact]
    public void Explicitly_overridable_rule_accepts_safe_stricter_parameters()
    {
        var local = Rule(Procurement, layer: RuleLayer.Tenant, parameters: new() { ["threshold"] = "10000" });
        RuleResolver.Default.Resolve([Rule(Procurement, parameters: new() { ["threshold"] = "25000" }, roles: [ApproverRole.FinanceDirector]), local], Date)
            .Rules.Should().Equal(local);
    }

    [Theory]
    [InlineData("0.20", true)]     // warns earlier: stricter
    [InlineData("0.05", false)]    // warns later: weaker
    [InlineData("1.50", false)]    // not a share
    public void Direction_of_a_parameter_is_declared_by_its_rule(string localPct, bool accepted)
    {
        var upper = Rule("BUDGET_LOW_REMAINING", severity: Severity.Warning, parameters: new() { ["pct"] = "0.10" }, roles: [ApproverRole.FinanceDirector]);
        var local = Rule("BUDGET_LOW_REMAINING", layer: RuleLayer.Tenant, severity: Severity.Warning, parameters: new() { ["pct"] = localPct });
        var resolve = () => RuleResolver.Default.Resolve([upper, local], Date);
        if (accepted) resolve().Rules.Should().Equal(local);
        else resolve.Should().Throw<ValidationException>();
    }

    [Fact]
    public void A_parameter_the_code_does_not_declare_cannot_be_changed_by_a_local_layer() =>
        FluentActions.Invoking(() => RuleResolver.Default.Resolve([
            Rule(parameters: new() { ["limit"] = "10" }, roles: [ApproverRole.FinanceDirector]),
            Rule(layer: RuleLayer.Tenant, parameters: new() { ["limit"] = "5" })], Date))
            .Should().Throw<ValidationException>().WithMessage("*limit*");

    [Fact]
    public void Fingerprint_is_order_independent_and_tracks_all_content()
    {
        var a = Rule(parameters: new() { ["b"] = "2", ["a"] = "1" });
        var b = Rule("B");
        var first = RuleResolver.Default.Resolve([a, b], Date);
        RuleResolver.Default.Resolve([b, a], Date).Fingerprint.Should().Be(first.Fingerprint);
        RuleResolver.Default.Resolve([Rule(parameters: new() { ["a"] = "1", ["b"] = "2" }), b], Date)
            .Fingerprint.Should().Be(first.Fingerprint);
        RuleResolver.Default.Resolve([Rule(parameters: new() { ["a"] = "9", ["b"] = "2" }), b], Date)
            .Fingerprint.Should().NotBe(first.Fingerprint);
        RuleResolver.Default.Resolve([Rule(message: "changed"), b], Date).Fingerprint.Should().NotBe(first.Fingerprint);
        first.Fingerprint.Should().HaveLength(64);
        first.Versions.Fingerprint.Should().Be(first.Fingerprint);
        first.Versions.AppliedRules.Should().HaveCount(2);
    }

    [Fact]
    public void Inputs_and_exposed_collections_are_immutable_snapshots()
    {
        var parameters = new Dictionary<string, string> { ["threshold"] = "25000" };
        var roles = new[] { ApproverRole.FinanceDirector };
        var rule = Rule(parameters: parameters, roles: roles);
        var candidates = new List<RuleDefinition> { rule };
        var set = RuleResolver.Default.Resolve(candidates, Date);
        parameters["threshold"] = "1";
        roles[0] = ApproverRole.BudgetOfficer;
        candidates.Clear();
        rule.DecimalParameter("threshold").Should().Be(25000);
        rule.OverridableBy.Should().Equal(ApproverRole.FinanceDirector);
        set.Rules.Should().Equal(rule);
        FluentActions.Invoking(() => ((IDictionary<string, string>)rule.Parameters).Add("x", "1")).Should().Throw<NotSupportedException>();
        FluentActions.Invoking(() => ((IList<RuleDefinition>)set.Rules).Clear()).Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Equivalent_resolutions_have_value_equal_versions()
    {
        var first = RuleResolver.Default.Resolve([Rule()], Date).Versions;
        var second = RuleResolver.Default.Resolve([Rule()], Date).Versions;
        (first == second).Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
        (first == first with { Tenant = 9 }).Should().BeFalse();
    }

    [Theory]
    [InlineData("PROCUREMENT_THRESHOLD", "threshold", "25000", "50000")]
    [InlineData("PO_LIQUIDATION", "tolerance_pct", "0.05", "0.10")]
    public void Explicit_override_still_cannot_weaken_parameter(string id, string key, string baseline, string local) =>
        FluentActions.Invoking(() => RuleResolver.Default.Resolve([
            Rule(id, parameters: new() { [key] = baseline }, roles: [ApproverRole.FinanceDirector]),
            Rule(id, layer: RuleLayer.Tenant, parameters: new() { [key] = local })], Date))
            .Should().Throw<ValidationException>();

    [Fact]
    public void Nonoverridable_guard_cannot_acquire_override_roles() =>
        FluentActions.Invoking(() => RuleResolver.Default.Resolve([
            Rule(), Rule(layer: RuleLayer.Tenant, roles: [ApproverRole.FinanceDirector])], Date))
            .Should().Throw<ValidationException>();

    [Fact]
    public void Missing_guard_parameter_is_not_a_safe_override() =>
        FluentActions.Invoking(() => RuleResolver.Default.Resolve([
            Rule(parameters: new() { ["threshold"] = "25000" }, roles: [ApproverRole.FinanceDirector]),
            Rule(layer: RuleLayer.Tenant)], Date)).Should().Throw<ValidationException>();

    [Theory]
    [InlineData("ruleId")]
    [InlineData("version")]
    [InlineData("layer")]
    [InlineData("step")]
    [InlineData("fund")]
    [InlineData("grant")]
    [InlineData("severity")]
    [InlineData("dynamicSeverity")]
    [InlineData("enabled")]
    [InlineData("from")]
    [InlineData("to")]
    [InlineData("message")]
    [InlineData("resolution")]
    [InlineData("roles")]
    public void Fingerprint_changes_when_an_effective_field_changes(string field)
    {
        var changed = field switch
        {
            "ruleId" => Rule("Y"),
            "version" => Rule(version: 2),
            "layer" => Rule(layer: RuleLayer.Federal),
            "step" => Rule(step: ValidationStep.RequiredSegments),
            "fund" => Rule(fund: "101"),
            "grant" => Rule(grant: "G"),
            "severity" => Rule(severity: Severity.Warning),
            "dynamicSeverity" => Rule(severity: null),
            "enabled" => Rule(enabled: false),
            "from" => Rule(from: Date.AddDays(-1)),
            "to" => Rule(to: Date),
            "message" => Rule(message: "different message"),
            "resolution" => Rule(resolution: "different resolution"),
            "roles" => Rule(roles: [ApproverRole.FinanceDirector]),
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
        RuleResolver.Default.Resolve([changed], Date, "101", "G").Fingerprint.Should()
            .NotBe(RuleResolver.Default.Resolve([Rule()], Date, "101", "G").Fingerprint);
    }

    [Fact]
    public void Versions_include_every_selected_rule_not_only_layer_maxima()
    {
        var set = RuleResolver.Default.Resolve([Rule("A", version: 2), Rule("B", version: 7),
            Rule("C", layer: RuleLayer.Federal, version: 3), Rule("D", layer: RuleLayer.State, version: 4),
            Rule("E", layer: RuleLayer.Tenant, version: 5)], Date);
        set.Versions.Engine.Should().Be(RuleResolver.EngineVersion);
        set.Versions.Core.Should().Be(7);
        set.Versions.Federal.Should().Be(3);
        set.Versions.State.Should().Be(4);
        set.Versions.Tenant.Should().Be(5);
        set.Versions.AppliedRules.Select(rule => (rule.RuleId, rule.Version))
            .Should().Equal(("A", 2), ("B", 7), ("C", 3), ("D", 4), ("E", 5));
    }

    [Fact]
    public void Applied_version_list_is_copied_and_read_only()
    {
        var list = new List<AppliedRuleVersion> { new("X", RuleLayer.Core, 1, null, null) };
        var versions = new RuleSetVersions("engine", 1, 0, 0, 0) { AppliedRules = list };
        list.Clear();
        versions.AppliedRules.Should().ContainSingle();
        FluentActions.Invoking(() => ((IList<AppliedRuleVersion>)versions.AppliedRules).Clear())
            .Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Override_role_order_does_not_change_fingerprint()
    {
        var first = Rule(roles: [ApproverRole.BudgetOfficer, ApproverRole.FinanceDirector]);
        var second = Rule(roles: [ApproverRole.FinanceDirector, ApproverRole.BudgetOfficer]);
        RuleResolver.Default.Resolve([first], Date).Fingerprint.Should().Be(RuleResolver.Default.Resolve([second], Date).Fingerprint);
    }
    [Fact]
    public void Mixed_scope_subject_retains_exact_selections_and_combined_fingerprint()
    {
        var subject = new SubjectBuilder()
            .With(SubjectBuilder.Distribution(1, "101-6000-53100", 10))
            .With(SubjectBuilder.Distribution(2, "701-6000-53100-G-COPS-26", 20)).Build();
        var global = Rule(Procurement, parameters: new() { ["threshold"] = "25000" }, roles: [ApproverRole.FinanceDirector]);
        var scoped = Rule(Procurement, layer: RuleLayer.Tenant, fund: "701", grant: "G-COPS-26",
            parameters: new() { ["threshold"] = "10000" });
        var union = RuleResolver.Default.ResolveForSubject([global, scoped], subject);
        union.Rules.Should().HaveCount(2);
        union.ForScope("101", null).Rules.Should().Equal(global);
        union.ForScope("701", "G-COPS-26").Rules.Should().Equal(scoped);
        union.Find(Procurement, "701", "G-COPS-26").Should().BeSameAs(scoped);
        FluentActions.Invoking(() => union.Find(Procurement)).Should().Throw<ValidationException>();
        FluentActions.Invoking(() => union.ForScope("999", null)).Should().Throw<ValidationException>();
        var reversed = subject with { Distributions = subject.Distributions.Reverse().ToArray() };
        RuleResolver.Default.ResolveForSubject([scoped, global], reversed).Fingerprint.Should().Be(union.Fingerprint);
        union.Fingerprint.Should().NotBe(RuleResolver.Default.ResolveForSubject([global], subject).Fingerprint);
        union.Versions.AppliedRules.Should().HaveCount(2);
    }

    [Fact]
    public void Disjoint_scopes_may_use_same_rule_layer_and_version()
    {
        var subject = new SubjectBuilder()
            .With(SubjectBuilder.Distribution(1, "101-6000-53100", 10))
            .With(SubjectBuilder.Distribution(2, "701-6000-53100-G-COPS-26", 20)).Build();
        var first = Rule(fund: "101");
        var second = Rule(fund: "701", grant: "G-COPS-26");
        var set = RuleResolver.Default.ResolveForSubject([first, second], subject);
        set.Rules.Should().Equal(first, second);
        RuleResolver.Default.ResolveForSubject([second, first], subject with { Distributions = subject.Distributions.Reverse().ToArray() })
            .Fingerprint.Should().Be(set.Fingerprint);
    }

    [Fact]
    public void Disabled_scoped_latest_version_does_not_reappear_from_union()
    {
        var subject = new SubjectBuilder()
            .With(SubjectBuilder.Distribution(1, "101-6000-53100", 10))
            .With(SubjectBuilder.Distribution(2, "701-6000-53100-G-COPS-26", 20)).Build();
        var set = RuleResolver.Default.ResolveForSubject([Rule(), Rule(version: 2, fund: "701", enabled: false)], subject);
        set.Rules.Should().ContainSingle();
        set.ForScope("701", "G-COPS-26").Rules.Should().BeEmpty();
        set.ForScope("101", null).Rules.Should().ContainSingle();
        set.Fingerprint.Should().NotBe(RuleResolver.Default.ResolveForSubject([Rule()], subject).Fingerprint);
    }
    [Fact]
    public void Invalid_and_missing_parameters_raise_domain_exception()
    {
        FluentActions.Invoking(() => Rule().Parameter("missing")).Should().Throw<ValidationException>();
        FluentActions.Invoking(() => Rule(parameters: new() { ["x"] = "bad" }).DecimalParameter("x")).Should().Throw<ValidationException>();
    }
}




public class RuleFingerprintGoldenTests
{
    [Fact]
    public void Fingerprint_bytes_are_stable()
    {
        var subject = SubjectBuilder.Exercise();
        RuleResolver.Default.Resolve(DemoRules.All(), SubjectBuilder.June15).Fingerprint.Should().Be("25CE68EAA113724768391B596CA5F0802E69FABFDAA5B2DFDB89E66D2FE5E1E3");
        RuleResolver.Default.ResolveForSubject(DemoRules.All(), subject).Fingerprint.Should().Be("B548844B89DF2A5A95BFD84872FCD2465E261EF253027CF66FACCF78E6601046");
    }
}
