using System.Globalization;
using System.Runtime.CompilerServices;
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.Exceptions;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.RuleSupport;

/// <summary>
/// A verdict with its evidence: the facts the rule looked at (inputs) and the figures it computed.
/// A fact is named after the expression that produced it: <c>.Input(invoice.Total)</c> is stored as "total".
/// When the stored name differs from the expression, InputAs / ComputedAs name it explicitly.
/// </summary>
public sealed class Finding
{
    private readonly Verdict _verdict;
    private readonly int? _line;
    private readonly Dictionary<string, string> _inputs = [];
    private readonly Dictionary<string, string> _computed = [];

    internal Finding(Verdict verdict, int? line)
    {
        _verdict = verdict;
        _line = line;
    }

    public Finding Input(object value, [CallerArgumentExpression(nameof(value))] string expression = "") =>
        InputAs(NameOf(expression), value);

    public Finding InputAs(string name, object value)
    {
        _inputs[name] = Format(value);
        return this;
    }

    public Finding Computed(object value, [CallerArgumentExpression(nameof(value))] string expression = "") =>
        ComputedAs(NameOf(expression), value);

    public Finding ComputedAs(string name, object value)
    {
        _computed[name] = Format(value);
        return this;
    }

    internal RuleOutcome ToOutcome(RuleDefinition definition, Severity configured) =>
        RuleOutcome.From(definition, _verdict.Severity ?? configured, _line, _inputs, _computed, _verdict.ReasonCode);

    /// <summary>
    /// "invoice.Total" → "total", "threshold" → "threshold".
    /// </summary>
    private static string NameOf(string expression)
    {
        var path = expression.Trim().TrimEnd('!');
        var name = path[(path.LastIndexOf('.') + 1)..];
        if (name.Length == 0 || !char.IsLetter(name[0]) || !name.All(c => char.IsLetterOrDigit(c) || c == '_'))
        {
            throw new InvalidOperationException(Problem.Of(ValidationErrors.FactNeedsName, ("expression", expression)).ToString());
        }

        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static string Format(object value) => value switch
    {
        string text => text,
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        int number => number.ToString(CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };
}
