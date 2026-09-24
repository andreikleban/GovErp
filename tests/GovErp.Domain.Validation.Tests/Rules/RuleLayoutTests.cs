using System.Text.RegularExpressions;
using GovErp.Domain.Validation.DomainServices;

namespace GovErp.Domain.Validation.Tests.Rules;

/// <summary>Every catalog rule is written in the same layout: Scope, Decide, Evidence (see ValidationRule).</summary>
public class RuleLayoutTests
{
    private static readonly string RulesFolder = Path.Combine(RepositoryRoot(), "src", "GovErp.Domain.Validation", "DomainServices", "Rules");

    public static TheoryData<string> RuleFiles() => new(RuleCatalog.Default.MandatoryRuleIds
        .Select(id => RuleCatalog.Default.Find(id))
        .Where(rule => rule is not null)
        .Select(rule => rule!.GetType().Name + ".cs"));

    [Theory]
    [MemberData(nameof(RuleFiles))]
    public void Rule_check_is_marked_scope_decide_evidence_in_order(string file)
    {
        var source = File.ReadAllText(Path.Combine(RulesFolder, file));
        var scope = source.IndexOf("// Scope:", StringComparison.Ordinal);
        var decide = source.IndexOf("// Decide:", StringComparison.Ordinal);
        var evidence = source.IndexOf("// Evidence", StringComparison.Ordinal);

        scope.Should().BeGreaterThan(0, $"{file} marks what it checks");
        decide.Should().BeGreaterThan(scope, $"{file} decides after the scope");
        evidence.Should().BeGreaterThan(decide, $"{file} records evidence after the decision");
        Regex.Count(source, @"// (Scope:|Decide:|Evidence)").Should().Be(3, $"{file} has one section of each kind");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GovErp.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("GovErp.sln not found above the test output folder.");
    }
}
