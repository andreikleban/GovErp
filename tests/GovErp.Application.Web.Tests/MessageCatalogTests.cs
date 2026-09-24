using System.Reflection;
using System.Text.RegularExpressions;
using GovErp.Application.Web.Common;
using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.ValueObjects;
using GovErp.Infrastructure.Seed;

namespace GovErp.Application.Web.Tests;

/// <summary>Every problem and reason code in the code has a message, and every message belongs to a code.</summary>
public partial class MessageCatalogTests
{
    private static readonly Assembly[] Sources =
    [
        typeof(Problem).Assembly,
        typeof(Domain.ChartOfAccounts.Exceptions.ChartOfAccountsErrors).Assembly,
        typeof(Domain.Ledger.Exceptions.LedgerErrors).Assembly,
        typeof(Domain.Payables.Exceptions.PayablesErrors).Assembly,
        typeof(ValidationPipeline).Assembly,
        typeof(AppErrors).Assembly,
    ];

    /// <summary>Codes are string constants shaped "AREA.NAME" (public catalogs and the private reason codes of the rules).</summary>
    private static IReadOnlyList<string> Codes() => Sources
        .SelectMany(a => a.GetTypes())
        .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
        .Where(f => f is { IsLiteral: true } && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .Where(value => CodeShape().IsMatch(value))
        .Distinct()
        .Order(StringComparer.Ordinal)
        .ToList();

    [Fact]
    public void Every_code_has_a_message()
    {
        var codes = Codes();
        codes.Should().HaveCountGreaterThan(150);
        codes.Where(code => Messages.Template(code) is null).Should().BeEmpty("each code needs an entry in Messages.resx");
    }

    [Fact]
    public void Every_message_belongs_to_a_code()
    {
        var codes = Codes().ToHashSet(StringComparer.Ordinal);
        var catalog = System.Xml.Linq.XDocument.Load(typeof(MessageCatalogTests).Assembly.Location is var _ ? CatalogPath() : "")
            .Descendants("data").Select(d => (string)d.Attribute("name")!);
        catalog.Where(name => !codes.Contains(name)).Should().BeEmpty("a message without a code is dead text");
    }

    [Fact]
    public async Task Rule_outcomes_render_with_every_placeholder_filled()
    {
        var data = SpringfieldData.Create();
        var subject = await ValidationSubjectAssemblerTests.Assembler(data).BuildAsync(data.NonPoExerciseInvoice(), SpringfieldData.Jun15);
        var record = new ValidationPipeline(RuleCatalog.Default).Evaluate(subject, data.Rules,
            EvaluationTrigger.Manual, SpringfieldData.ClerkId, DateTimeOffset.UnixEpoch);

        record.Outcomes.Should().NotBeEmpty();
        foreach (var outcome in record.Outcomes)
        {
            Messages.Render(outcome).Should().NotContain("{", outcome.ReasonCode).And.NotBe(outcome.ReasonCode);
        }

        Messages.Render(Problem.Of("NO.SUCH_CODE")).Should().Be("NO.SUCH_CODE", "an unknown code stays visible");
    }

    private static string CatalogPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GovErp.sln")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "src", "GovErp.Application.Web", "Common", "Messages.resx");
    }

    [GeneratedRegex(@"^[A-Z][A-Z_]*\.[A-Z][A-Z_]*$")]
    private static partial Regex CodeShape();
}
