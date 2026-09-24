using GovErp.Application.Web.Common;
using GovErp.Application.Web.Reference.Contracts;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Application.Web.Reference;

/// <summary>
/// "Role → action" built from the same constants and rules the services check, not a separate hand-made table (spec §6):
/// <list type="bullet">
/// <item>create and submit: Roles.ApClerk (InvoiceAppService);</item>
/// <item>approve: Roles.Approvers (ApprovalAppService.ApproveAsync);</item>
/// <item>release a soft stop: Roles.Overriders that some rule in force lists in OverridableBy. ApprovalAppService.OverrideAsync
/// checks outcome.OverridableBy the same way; a rule whose severity is computed per outcome (BUDGET_AVAILABILITY) still lists
/// overriders only when it can be a soft stop;</item>
/// <item>amend the budget: Roles.Overriders (BudgetAppService.AmendAsync);</item>
/// <item>post and set a payment hold: Roles.Posters (PostingAppService.PostAsync, InvoiceAppService.SetPaymentHoldAsync).</item>
/// </list>
/// </summary>
internal static class RoleMatrix
{
    private static readonly string[] RoleOrder = [Roles.ApClerk, Roles.DepartmentHead, Roles.GrantsManager, Roles.BudgetOfficer, Roles.FinanceDirector];

    private const string SeparationOfDuties =
        "The author of an invoice never approves, rejects, releases a soft stop on, or posts their own invoice, "
        + "even when their role otherwise allows the action (separation of duties, enforced by the server on every such command).";

    public static RoleMatrixVm From(EffectiveRuleSet rulesInForce)
    {
        var releasers = Roles.Overriders
            .Where(role => rulesInForce.Rules.Any(rule => rule.OverridableBy.Select(RoleMapping.ToRoleName).Contains(role, StringComparer.Ordinal)))
            .ToList();

        var rows = RoleOrder.Select(role => new RoleMatrixRowVm(role,
            CanCreateAndSubmit: role == Roles.ApClerk,
            CanApprove: Roles.Approvers.Contains(role),
            CanReleaseSoftStop: releasers.Contains(role),
            CanAmendBudget: Roles.Overriders.Contains(role),
            CanPost: Roles.Posters.Contains(role),
            CanPaymentHold: Roles.Posters.Contains(role))).ToList();

        return new RoleMatrixVm(rows, releasers, SeparationOfDuties);
    }
}
