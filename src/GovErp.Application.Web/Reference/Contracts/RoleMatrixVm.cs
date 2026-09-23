namespace GovErp.Application.Web.Reference.Contracts;

/// <summary>
/// ReleaseSoftStopRoles — «кто может снять», из тех же ролей и правил, что и CanReleaseSoftStop в Rows (не отдельная догадка).
/// SeparationOfDutiesNote — правило разделения обязанностей текстом под матрицей (проверяется сервисами, не самой матрицей).
/// </summary>
public sealed record RoleMatrixVm(IReadOnlyList<RoleMatrixRowVm> Rows, IReadOnlyList<string> ReleaseSoftStopRoles, string SeparationOfDutiesNote);
