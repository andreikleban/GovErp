namespace GovErp.Application.Web.Reference.Contracts;

/// <summary>Строка матрицы «роль → действие»; столбцы — spec §6 (Setup › Users и Roles).</summary>
public sealed record RoleMatrixRowVm(string Role, bool CanCreateAndSubmit, bool CanApprove, bool CanReleaseSoftStop,
    bool CanAmendBudget, bool CanPost, bool CanPaymentHold);
