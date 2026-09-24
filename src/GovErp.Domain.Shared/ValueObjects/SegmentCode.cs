using System.Text.RegularExpressions;

namespace GovErp.Domain.Shared.ValueObjects;

/// <summary>
/// Base of a chart segment code: a non-empty string in that segment's format.
/// </summary>
public abstract record SegmentCode
{
    public string Value { get; }

    protected SegmentCode(string value, string pattern, string segmentName)
    {
        if (value is null || !Regex.IsMatch(value, pattern))
            throw new InvalidValueException(nameof(value), ValueErrors.SegmentFormat, ("segment", segmentName), ("value", value), ("pattern", pattern));
        Value = value;
    }

    public sealed override string ToString() => Value;
}
