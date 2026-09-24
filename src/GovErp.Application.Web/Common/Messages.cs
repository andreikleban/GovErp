using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Application.Web.Common;

/// <summary>
/// The only place where problems become text for people: code → template in Messages.resx, "{name}" → the argument.
/// An unknown code is shown as is, so a missing template is visible rather than silent (a test keeps the catalog complete).
/// </summary>
public static partial class Messages
{
    private static readonly ResourceManager Templates = new("GovErp.Application.Web.Common.Messages", typeof(Messages).Assembly);

    public static string Render(Problem problem) => Render(problem.Code, problem.Args);

    /// <summary>
    /// A rule outcome: its reason code with the facts it recorded (inputs and computed figures) as arguments.
    /// </summary>
    public static string Render(RuleOutcome outcome) =>
        Render(outcome.ReasonCode, outcome.Inputs.Concat(outcome.Computed).GroupBy(f => f.Key).ToDictionary(g => g.Key, g => g.First().Value));

    public static string Render(string code, IReadOnlyDictionary<string, string> args)
    {
        var template = Template(code);
        return template is null ? code : Placeholder().Replace(template, m => args.TryGetValue(m.Groups[1].Value, out var value) ? value : m.Value);
    }

    public static string? Template(string code) => Templates.GetString(code, CultureInfo.CurrentUICulture);

    /// <summary>
    /// The argument names a template uses.
    /// </summary>
    public static IReadOnlyList<string> Placeholders(string template) =>
        Placeholder().Matches(template).Select(m => m.Groups[1].Value).Distinct().ToList();

    [GeneratedRegex(@"\{([A-Za-z][A-Za-z0-9]*)\}")]
    private static partial Regex Placeholder();
}
