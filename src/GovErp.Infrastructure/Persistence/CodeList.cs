using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GovErp.Infrastructure.Persistence;

/// <summary>
/// List of segment codes ↔ the string "a,b,c".
/// </summary>
internal static class CodeList
{
    public static ValueConverter<IReadOnlyList<T>, string> Converter<T>(Func<string, T> parse) where T : SegmentCode =>
        new(v => string.Join(',', v.Select(c => c.Value)),
            s => s.Length == 0 ? new List<T>() : s.Split(',', StringSplitOptions.None).Select(parse).ToList());

    public static ValueComparer<IReadOnlyList<T>> Comparer<T>() =>
        new((a, b) => a!.SequenceEqual(b!), v => v.Aggregate(0, (h, x) => HashCode.Combine(h, x)), v => v.ToList());
}
