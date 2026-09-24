using GovErp.Application.Web.Reference.Contracts;
using GovErp.Domain.Validation.DomainServices;

namespace GovErp.Application.Web.Reference;

/// <summary>
/// Описания правил каталога движка. Текст сверен с классами в Domain.Validation/DomainServices/Rules:
/// при изменении логики правила меняется и описание (тест требует описание для каждого обязательного правила).
/// «Source» честно разделяет: требование задания, типичный контроль AP, допущение демо.
/// </summary>
public static class RuleDescriptions
{
    private const string Assignment = "Exercise requirement";
    private const string TypicalControl = "Typical AP control";
    private const string DemoAssumption = "Demo assumption";

    private static readonly IReadOnlyDictionary<string, RuleDescriptionVm> ById = new[]
    {
        Rule("SEG_REQUIRED", 1, "Required segments", "Grant segment required by the fund",
            "Every distribution charged to a fund whose grant policy is Required must carry a grant segment. Fund, department and object are always present because the account code format requires them.",
            ["Fund code and its grant policy", "Account code of the line"],
            new Dictionary<string, string>(),
            "Hard Stop on the line: the fund requires a grant segment.",
            "Cannot be overridden. Recode the line with a valid grant of this fund.",
            $"{Assignment}: \"Grants Fund requires a valid grant segment.\"",
            ["Which funds require a grant or project segment?", "Is a program/project segment needed in addition to the grant segment?"]),

        Rule("SEG_GRANT_FORBIDDEN", 1, "Required segments", "Grant segment not accepted by the fund",
            "A distribution that carries a grant segment is rejected when its fund's grant policy is Forbidden (for example the General Fund in this demo).",
            ["Fund code and its grant policy", "Grant segment of the line"],
            new Dictionary<string, string>(),
            "Hard Stop on the line: the fund does not accept a grant segment.",
            "Cannot be overridden. Remove the grant segment or charge the grants fund.",
            $"{DemoAssumption}: the inverse of the exercise's grant-segment rule.",
            ["May a general fund carry grant tracking, for example for local matching shares?"]),

        Rule("COA_COMBINATION_ACTIVE", 2, "Valid combination", "Account combination exists and is active",
            "The full account combination (fund-department-object[-grant]) must exist in the chart of accounts and be active on the invoice date.",
            ["Account combination of the line", "Combination status and effective dates", "Invoice date"],
            new Dictionary<string, string>(),
            "Hard Stop on the line: the combination does not exist or is not active on that date.",
            "Cannot be overridden. Use an active combination, or ask finance to approve a new one.",
            $"{Assignment}: validation step 2, valid chart of accounts combination.",
            ["Are combinations a pre-approved list, or generated dynamically under cross-validation rules?", "Who approves a new combination and how quickly?"]),

        Rule("FUND_DEPT_OBJECT_ALLOWED", 3, "Fund and grant restrictions", "Department and object are an allowed use of the fund",
            "The fund must be active, and the line's department and object must be in the fund's allowed lists (an empty list allows all).",
            ["Fund status", "Fund's allowed departments and objects", "Department and object of the line"],
            new Dictionary<string, string>(),
            "Hard Stop on the line: the fund is inactive, or the department or object is not an allowed use of it.",
            "Cannot be overridden. Recode to a fund that allows this use.",
            $"{Assignment}: fund restrictions (step 3). The allowed lists are demo data.",
            ["Which restrictions come from law, enabling legislation or bond covenants, and which are local policy?"]),

        Rule("GRANT_ELIGIBLE", 3, "Fund and grant restrictions", "Cost is allowable under the grant",
            "For lines with a grant: the grant must be active, the service date (or invoice date) must fall within the grant period, and the department and object must be covered by the grant.",
            ["Grant status and period", "Grant's allowed departments and allowable objects", "Service date", "Department and object of the line"],
            new Dictionary<string, string>(),
            "Hard Stop on the line: inactive grant, date outside the grant period, or department/object not covered.",
            "Cannot be overridden. Recode to an eligible grant or to a non-grant fund.",
            $"{Assignment}: grant restrictions (step 3). Analogous to allowable-cost rules of grant awards; the grant data is demo data.",
            ["Is eligibility based on the period of performance, the obligation date or the service date?", "Are pre-award or closeout costs allowed?"]),

        Rule("VENDOR_ELIGIBLE", 4, "Transaction purpose", "Vendor may be paid",
            "The vendor must be active and not debarred. When any line is charged to a federal grant, the vendor must also be registered in SAM.gov (demo policy).",
            ["Vendor status, debarment flag and SAM registration", "Whether any line is charged to a federal grant"],
            new Dictionary<string, string>(),
            "Hard Stop on the invoice: inactive, debarred, or not SAM-registered for a federal grant payment.",
            "Cannot be overridden. Resolve the vendor's status before submitting.",
            $"{TypicalControl} (exclusion check before paying federal funds). Requiring SAM registration for every vendor is a {DemoAssumption.ToLowerInvariant()}.",
            ["Is the requirement an exclusion (debarment) check only, or SAM registration as well?", "When must the check be performed: at contract, at invoice, or at payment?"]),

        Rule("PROCUREMENT_THRESHOLD", 4, "Transaction purpose", "Large purchase without a purchase order",
            "A non-PO invoice whose total is at or above the threshold needs a purchase order or a documented procurement exception. PO-backed invoices are not checked.",
            ["Invoice total", "Whether the invoice is PO-backed"],
            new Dictionary<string, string> { ["threshold"] = "Invoice total (USD) from which a purchase order is expected." },
            "Soft Stop on the invoice: the total meets the threshold and there is no purchase order.",
            "A role listed in \"Overridable by\" releases the stop with a reason, or the invoice is recoded against a purchase order.",
            $"{DemoAssumption}: modeled on procurement thresholds in state and local rules; 25,000 is not a legal value.",
            ["What are the actual thresholds (state law, local ordinance)?", "Should related invoices be aggregated to detect split purchases?"]),

        Rule("INVOICE_DUPLICATE", 4, "Transaction purpose", "Duplicate vendor invoice number",
            "Another invoice from the same vendor with the same normalized invoice number (trimmed, case-insensitive) already exists.",
            ["Vendor", "Normalized vendor invoice number"],
            new Dictionary<string, string>(),
            "Hard Stop on the invoice: possible duplicate payment.",
            "Cannot be overridden. Verify the invoice was not already entered; correct the number if it was a typo.",
            $"{TypicalControl}: duplicate-payment prevention.",
            ["Should near-duplicates (same amount and date, different number) be flagged as well?"]),

        Rule("BUDGET_AVAILABILITY", 5, "Budget availability", "Budget is available for the new spending",
            "Per budget line (account and fiscal year): available = amended - actuals - encumbered - held. The invoice needs new budget for its amount on that account minus what it liquidates from a purchase order; its own reservation is added back. The check fails when the available amount after the invoice is negative.",
            ["Amended budget, actuals, encumbrances and holds of the line", "Invoice amount on the account", "PO liquidation", "Fund budget control (hard or soft)"],
            new Dictionary<string, string>(),
            "Hard Stop when the fund uses hard budget control or no budget line exists; Soft Stop when the fund uses soft control.",
            "Hard Stop: budget amendment, budget transfer, grant-budget revision or an authorized coding change. Soft Stop: may also be released by a listed role with a reason.",
            $"{Assignment}: hard budget control and the $13,000 overage (375,000 - 132,000 - 96,000 = 147,000 available).",
            ["At which level is budget controlled: account, department or fund roll-up?", "Does a soft-control overage need council notification or later ratification?"]),

        Rule("BUDGET_LOW_REMAINING", 5, "Budget availability", "Little budget remains after the invoice",
            "After the invoice, the remaining available budget divided by the amended budget is below the percentage. Only informative; nothing is blocked.",
            ["Amended budget and available budget of the line", "New budget required by the invoice"],
            new Dictionary<string, string> { ["pct"] = "Share of the amended budget (0 to 1) below which a warning is shown." },
            "Warning on the line.",
            "No action required; consider a budget review.",
            $"{DemoAssumption}: shows the Warning state; not a regulatory requirement.",
            ["Is an early-warning threshold useful, and should it differ by fund type?"]),

        Rule("PO_LIQUIDATION", 6, "Encumbrance impact", "Invoice against a purchase order line",
            "For each PO line: the invoice first liquidates the line's unclaimed encumbrance; any excess needs new budget (checked by BUDGET_AVAILABILITY). Cumulative billing on the line (already posted + other open invoices + this invoice) is compared with the authorized PO amount.",
            ["PO line: authorized amount, remaining encumbrance, already posted, other open claims", "Invoice amount on the PO line"],
            new Dictionary<string, string> { ["tolerance_pct"] = "Allowed cumulative billing above the authorized PO amount, as a share (0 to 1)." },
            "Allowed when fully covered by the encumbrance; Warning when part of the amount needs new budget or billing is above the PO within tolerance; Hard Stop when cumulative billing exceeds the tolerance or the PO line is missing or closed.",
            "Hard Stop: a PO change order. Warning: no action required.",
            $"{Assignment}: behaviour of a PO-backed invoice that liquidates an encumbrance. The 5% tolerance is a {DemoAssumption.ToLowerInvariant()}.",
            ["Is the tolerance a percentage, an absolute cap, or both?", "Is receipt matching (two- or three-way match) required before payment?"]),

        Rule(RuleCatalog.ApprovalRouteRuleId, 7, "Approval requirements", "Approval route",
            "Not a check: parameters for building the approval route. The route includes the department head of every charged department, the grants manager when any line is grant-funded, the finance director when the total reaches the threshold, and the role that must release each open soft stop.",
            ["Departments and grants on the lines", "Invoice total", "Open soft stops"],
            new Dictionary<string, string> { ["finance_director_threshold"] = "Invoice total (USD) from which the finance director must approve." },
            "Never fires; it shapes the route shown on the invoice.",
            "Approvals are recorded by the listed roles; the author never approves their own invoice.",
            $"{DemoAssumption}: local approval policy; 50,000 is not a legal value.",
            ["What are the approval limits by role, and is delegation allowed?", "Do grant-funded invoices need dual approval?"]),
    }.ToDictionary(d => d.RuleId, StringComparer.Ordinal);

    public static IReadOnlyCollection<RuleDescriptionVm> All => ById.Values.ToList();

    public static RuleDescriptionVm? Find(string ruleId) => ById.GetValueOrDefault(ruleId);

    private static RuleDescriptionVm Rule(string id, int step, string stepName, string title, string checks, string[] facts,
        Dictionary<string, string> parameters, string whenFired, string resolution, string source, string[] smeQuestions) =>
        new(id, step, stepName, title, checks, facts, parameters, whenFired, resolution, source, smeQuestions);
}
