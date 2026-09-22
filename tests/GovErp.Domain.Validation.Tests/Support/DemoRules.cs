using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Tests;

public static class DemoRules
{
    public static RuleDefinition Rule(string id, ValidationStep step, RuleLayer layer, Severity? severity,
        Dictionary<string, string>? parameters = null, ApproverRole[]? overridableBy = null, int version = 1,
        DateOnly? from = null, DateOnly? to = null, bool enabled = true, string? scopeFund = null, string? scopeGrant = null) =>
        new(id, version, step, layer, severity, parameters ?? [], overridableBy ?? [], from ?? new DateOnly(2025, 7, 1), to,
            $"{id} fired", $"Resolve {id}", enabled, scopeFund, scopeGrant);

    public static IReadOnlyList<RuleDefinition> All() =>
    [
        Rule("SEG_REQUIRED", ValidationStep.RequiredSegments, RuleLayer.Core, Severity.HardStop),
        Rule("SEG_GRANT_FORBIDDEN", ValidationStep.RequiredSegments, RuleLayer.Core, Severity.HardStop),
        Rule("COA_COMBINATION_ACTIVE", ValidationStep.ValidCombination, RuleLayer.Core, Severity.HardStop),
        Rule("FUND_DEPT_OBJECT_ALLOWED", ValidationStep.FundAndGrantRestrictions, RuleLayer.Tenant, Severity.HardStop),
        Rule("GRANT_ELIGIBLE", ValidationStep.FundAndGrantRestrictions, RuleLayer.Federal, Severity.HardStop),
        Rule("VENDOR_ELIGIBLE", ValidationStep.TransactionPurpose, RuleLayer.Federal, Severity.HardStop),
        Rule("PROCUREMENT_THRESHOLD", ValidationStep.TransactionPurpose, RuleLayer.State, Severity.SoftStop,
            new() { ["threshold"] = "25000" }, [ApproverRole.FinanceDirector]),
        Rule("INVOICE_DUPLICATE", ValidationStep.TransactionPurpose, RuleLayer.Core, Severity.HardStop),
        Rule("BUDGET_AVAILABILITY", ValidationStep.BudgetAvailability, RuleLayer.Core, null,
            overridableBy: [ApproverRole.BudgetOfficer, ApproverRole.FinanceDirector]),
        Rule("BUDGET_LOW_REMAINING", ValidationStep.BudgetAvailability, RuleLayer.Tenant, Severity.Warning,
            new() { ["pct"] = "0.10" }),
        Rule("PO_LIQUIDATION", ValidationStep.EncumbranceImpact, RuleLayer.Core, null,
            new() { ["tolerance_pct"] = "0.05" }),
        Rule("APPROVAL_ROUTE", ValidationStep.ApprovalRequirements, RuleLayer.Tenant, null,
            new() { ["finance_director_threshold"] = "50000" }),
    ];
}
