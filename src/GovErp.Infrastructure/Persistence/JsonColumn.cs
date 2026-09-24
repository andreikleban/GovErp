using System.Text.Json;
using System.Text.Json.Serialization;
using GovErp.Domain.Validation.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GovErp.Infrastructure.Persistence;

/// <summary>
/// Serializes snapshots and routes into nvarchar(max) columns.
/// </summary>
public static class JsonColumn
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(), new MoneyJsonConverter(), new AccountCodeJsonConverter(), new RuleOutcomeJsonConverter(),
            new UserIdJsonConverter(), new ProblemJsonConverter(),
        },
    };

    public static PropertyBuilder<T> AsJson<T>(this PropertyBuilder<T> builder) =>
        builder.HasConversion(
                new ValueConverter<T, string>(v => JsonSerializer.Serialize(v, Options), s => JsonSerializer.Deserialize<T>(s, Options)!),
                new ValueComparer<T>(
                    (a, b) => JsonSerializer.Serialize(a, Options) == JsonSerializer.Serialize(b, Options),
                    v => JsonSerializer.Serialize(v, Options).GetHashCode(),
                    v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, Options), Options)!))
            .HasColumnType("nvarchar(max)");

    private sealed class MoneyJsonConverter : JsonConverter<Money>
    {
        public override Money Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => new(r.GetDecimal());
        public override void Write(Utf8JsonWriter w, Money v, JsonSerializerOptions o) => w.WriteNumberValue(v.Amount);
    }

    private sealed class AccountCodeJsonConverter : JsonConverter<AccountCode>
    {
        public override AccountCode Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => AccountCode.Parse(r.GetString()!);
        public override void Write(Utf8JsonWriter w, AccountCode v, JsonSerializerOptions o) => w.WriteStringValue(v.ToString());
    }

    /// <summary>
    /// Problem.Args is IReadOnlyDictionary, so STJ cannot construct it. Rows saved before codes store the reason as a JSON string.
    /// </summary>
    private sealed class ProblemJsonConverter : JsonConverter<Problem>
    {
        private sealed record Dto(string Code, Dictionary<string, string> Args);

        public override Problem Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o)
        {
            // An unknown code is shown as itself, so the old sentence still reaches the page.
            if (r.TokenType == JsonTokenType.String)
            {
                return new Problem(r.GetString() ?? "", new Dictionary<string, string>(StringComparer.Ordinal));
            }

            var d = JsonSerializer.Deserialize<Dto>(ref r, o)!;
            return new Problem(d.Code, d.Args ?? new Dictionary<string, string>(StringComparer.Ordinal));
        }

        public override void Write(Utf8JsonWriter w, Problem v, JsonSerializerOptions o) =>
            JsonSerializer.Serialize(w, new Dto(v.Code, new Dictionary<string, string>(v.Args, StringComparer.Ordinal)), o);
    }

    /// <summary>
    /// UserId is a struct: STJ creates it with the implicit default constructor and loses the value without a converter.
    /// </summary>
    private sealed class UserIdJsonConverter : JsonConverter<UserId>
    {
        public override UserId Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => new(r.GetGuid());
        public override void Write(Utf8JsonWriter w, UserId v, JsonSerializerOptions o) => w.WriteStringValue(v.Value);
    }

    /// <summary>
    /// RuleOutcome is created only from a RuleDefinition; from storage via RuleOutcome.Restore.
    /// </summary>
    private sealed class RuleOutcomeJsonConverter : JsonConverter<RuleOutcome>
    {
        private sealed record Dto(Guid OutcomeRef, string RuleId, int RuleVersion, ValidationStep Step, RuleLayer Layer, int? DistributionLine,
            Severity Severity, Dictionary<string, string> Inputs, Dictionary<string, string> Computed, string ReasonCode, string Resolution,
            List<ApproverRole> OverridableBy, OverrideSnapshot? OverriddenBy);

        public override RuleOutcome Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o)
        {
            var d = JsonSerializer.Deserialize<Dto>(ref r, o)!;
            return RuleOutcome.Restore(d.OutcomeRef, d.RuleId, d.RuleVersion, d.Step, d.Layer, d.DistributionLine, d.Severity,
                d.Inputs, d.Computed, d.ReasonCode, d.Resolution, d.OverridableBy, d.OverriddenBy);
        }

        public override void Write(Utf8JsonWriter w, RuleOutcome v, JsonSerializerOptions o) =>
            JsonSerializer.Serialize(w, new Dto(v.OutcomeRef, v.RuleId, v.RuleVersion, v.Step, v.Layer, v.DistributionLine, v.Severity,
                new(v.Inputs), new(v.Computed), v.ReasonCode, v.Resolution, [.. v.OverridableBy], v.OverriddenBy), o);
    }
}
