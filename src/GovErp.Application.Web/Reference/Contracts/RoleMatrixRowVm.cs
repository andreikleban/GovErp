namespace GovErp.Application.Web.Reference.Contracts;

/// <summary>
/// A row of the "role → action" matrix; the columns follow spec §6 (Setup › Users and Roles).
/// </summary>
public sealed record RoleMatrixRowVm(string Role, bool CanCreateAndSubmit, bool CanApprove, bool CanReleaseSoftStop,
    bool CanAmendBudget, bool CanPost, bool CanPaymentHold);
