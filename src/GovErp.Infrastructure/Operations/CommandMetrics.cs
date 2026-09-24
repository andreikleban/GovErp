using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace GovErp.Infrastructure.Operations;

/// <summary>
/// Counts completed commands in this process. The same increments feed the GovErp.Commands meter.
/// </summary>
public static class CommandMetrics
{
    public const string MeterName = "GovErp.Commands";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Executed = Meter.CreateCounter<long>("goverp.commands.executed");
    private static readonly ConcurrentDictionary<(string Command, string Status), long> Counts = new();

    public static void Record(string commandType, string status)
    {
        Counts.AddOrUpdate((commandType, status), 1, (_, count) => count + 1);
        Executed.Add(1, new KeyValuePair<string, object?>("command", commandType), new KeyValuePair<string, object?>("status", status));
    }

    public static IReadOnlyList<(string Command, string Status, long Count)> Snapshot() =>
        Counts.OrderBy(pair => pair.Key.Command, StringComparer.Ordinal).ThenBy(pair => pair.Key.Status, StringComparer.Ordinal)
            .Select(pair => (pair.Key.Command, pair.Key.Status, pair.Value)).ToList();
}
