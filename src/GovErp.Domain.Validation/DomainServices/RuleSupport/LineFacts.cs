using GovErp.Domain.Validation.Codes;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.RuleSupport;

/// <summary>
/// Facts the pipeline's input check guarantees before any rule runs (ValidationPipeline.MissingFacts).
/// </summary>
internal static class LineFacts
{
    public static FundSnapshot KnownFund(this DistributionSnapshot line) =>
        line.Fund ?? throw new InvalidOperationException(Problem.Of(InputErrors.FundFactsMissing, ("line", line.LineNo), ("account", line.Account)).ToString());

    /// <summary>
    /// For a line with a grant segment.
    /// </summary>
    public static GrantSnapshot KnownGrant(this DistributionSnapshot line) =>
        line.Grant ?? throw new InvalidOperationException(Problem.Of(InputErrors.GrantFactsMissing, ("line", line.LineNo), ("grant", line.Account.Grant)).ToString());
}
