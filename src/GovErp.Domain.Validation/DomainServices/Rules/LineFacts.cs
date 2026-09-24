using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

/// <summary>Facts the pipeline's input check guarantees before any rule runs (ValidationPipeline.MissingFacts).</summary>
internal static class LineFacts
{
    public static FundSnapshot KnownFund(this DistributionSnapshot line) =>
        line.Fund ?? throw new InvalidOperationException($"Line {line.LineNo} has no fund facts.");

    /// <summary>For a line with a grant segment.</summary>
    public static GrantSnapshot KnownGrant(this DistributionSnapshot line) =>
        line.Grant ?? throw new InvalidOperationException($"Line {line.LineNo} has no grant facts.");
}
