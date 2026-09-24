using System.Globalization;

namespace GovErp.Domain.Shared.ValueObjects;

/// <summary>
/// A business problem as data: a stable code and named arguments. The domain never builds text for people;
/// the application renders the message from its catalog (code → template with the argument names).
/// </summary>
public sealed record Problem(string Code, IReadOnlyDictionary<string, string> Args)
{
    public static Problem Of(string code, params (string Name, object? Value)[] args) =>
        new(code, args.ToDictionary(arg => arg.Name, arg => Format(arg.Value), StringComparer.Ordinal));

    /// <summary>
    /// For logs and exception messages, not for people: "CODE (name=value, ...)".
    /// </summary>
    public override string ToString() =>
        Args.Count == 0 ? Code : $"{Code} ({string.Join(", ", Args.Select(arg => $"{arg.Key}={arg.Value}"))})";

    private static string Format(object? value) => value switch
    {
        null => "",
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IFormattable number when value is int or long or decimal => number.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };
}

/// <summary>
/// A value that cannot be constructed from the given input; carries the problem instead of a sentence.
/// </summary>
public sealed class InvalidValueException(string parameter, string code, params (string Name, object? Value)[] args)
    : ArgumentException(Problem.Of(code, args).ToString(), parameter)
{
    public Problem Problem { get; } = Problem.Of(code, args);
}

/// <summary>
/// Codes of the shared value objects.
/// </summary>
public static class ValueErrors
{
    public const string AmountTooLarge = "VALUE.AMOUNT_TOO_LARGE";
    public const string FractionalCents = "VALUE.FRACTIONAL_CENTS";
    public const string SegmentFormat = "VALUE.SEGMENT_FORMAT";
    public const string AccountSegments = "VALUE.ACCOUNT_SEGMENTS";
    public const string PeriodOrder = "VALUE.PERIOD_ORDER";
    public const string EmptyUser = "VALUE.EMPTY_USER";
    public const string FiscalYearRange = "VALUE.FISCAL_YEAR_RANGE";
}
