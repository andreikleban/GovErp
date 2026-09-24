namespace GovErp.Application.Web.Reference.Contracts;

/// <summary>
/// ReleaseSoftStopRoles is "who can release", built from the same roles and rules as CanReleaseSoftStop in Rows (not a separate guess).
/// SeparationOfDutiesNote is the separation-of-duties rule as text under the matrix (enforced by the services, not by the matrix).
/// </summary>
public sealed record RoleMatrixVm(IReadOnlyList<RoleMatrixRowVm> Rows, IReadOnlyList<string> ReleaseSoftStopRoles, string SeparationOfDutiesNote);
