using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Infrastructure.Seed;

/// <summary>The 12 definitions of spec §4.2. Thresholds and layers are demo assumptions, not legal norms.</summary>
public static class RuleSeed
{
    public static IReadOnlyList<RuleDefinition> All(DateOnly from)
    {
        RuleDefinition R(string id, ValidationStep step, RuleLayer layer, Severity? severity, string message, string resolution,
            Dictionary<string, string>? p = null, ApproverRole[]? overridable = null) =>
            new(id, 1, step, layer, severity, p ?? [], overridable ?? [], from, null, message, resolution);

        return
        [
            R("SEG_REQUIRED", ValidationStep.RequiredSegments, RuleLayer.Core, Severity.HardStop,
                "A required chart-of-accounts segment is missing.", "Add the missing segment."),
            R("SEG_GRANT_FORBIDDEN", ValidationStep.RequiredSegments, RuleLayer.Core, Severity.HardStop,
                "The fund does not accept a grant segment.", "Remove the grant segment or use a grant fund."),
            R("COA_COMBINATION_ACTIVE", ValidationStep.ValidCombination, RuleLayer.Core, Severity.HardStop,
                "The account combination is not active.", "Use an active combination or request a new one from Finance."),
            R("FUND_DEPT_OBJECT_ALLOWED", ValidationStep.FundAndGrantRestrictions, RuleLayer.Tenant, Severity.HardStop,
                "The department or object is not an allowed use of the fund.", "Recode to a fund that permits this use."),
            R("GRANT_ELIGIBLE", ValidationStep.FundAndGrantRestrictions, RuleLayer.Federal, Severity.HardStop,
                "The expense is not eligible under the grant.", "Recode to an eligible grant or to a non-grant fund."),
            R("VENDOR_ELIGIBLE", ValidationStep.TransactionPurpose, RuleLayer.Federal, Severity.HardStop,
                "The vendor is not eligible for payment.", "Resolve vendor status before submitting."),
            R("PROCUREMENT_THRESHOLD", ValidationStep.TransactionPurpose, RuleLayer.State, Severity.SoftStop,
                "Non-PO invoice meets the procurement threshold.", "Attach a purchase order or a documented procurement exception.",
                new() { ["threshold"] = "25000" }, [ApproverRole.FinanceDirector]),
            R("INVOICE_DUPLICATE", ValidationStep.TransactionPurpose, RuleLayer.Core, Severity.HardStop,
                "Duplicate invoice number for this vendor.", "Verify the invoice was not already entered."),
            R("BUDGET_AVAILABILITY", ValidationStep.BudgetAvailability, RuleLayer.Core, null,
                "The invoice exceeds available budget.", "Budget amendment, budget transfer, or authorized coding change.",
                overridable: [ApproverRole.BudgetOfficer, ApproverRole.FinanceDirector]),
            R("BUDGET_LOW_REMAINING", ValidationStep.BudgetAvailability, RuleLayer.Tenant, Severity.Warning,
                "Little budget remains after this invoice.", "No action required; consider a budget review.",
                new() { ["pct"] = "0.10" }),
            R("PO_LIQUIDATION", ValidationStep.EncumbranceImpact, RuleLayer.Core, null,
                "PO liquidation and tolerance check.", "Within tolerance no action is required; above it a change order is required.",
                new() { ["tolerance_pct"] = "0.05" }),
            R("APPROVAL_ROUTE", ValidationStep.ApprovalRequirements, RuleLayer.Tenant, null,
                "Approval route parameters.", "—",
                new() { ["finance_director_threshold"] = "50000" }),
        ];
    }
}
