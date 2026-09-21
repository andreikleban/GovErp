using System.Text.RegularExpressions;

namespace GovErp.Domain.Shared.ValueObjects;

public abstract record SegmentCode
{
    public string Value { get; }

    protected SegmentCode(string value, string pattern, string segmentName)
    {
        if (value is null || !Regex.IsMatch(value, pattern))
            throw new ArgumentException($"{segmentName} code must match {pattern}.", nameof(value));
        Value = value;
    }

    public sealed override string ToString() => Value;
}
