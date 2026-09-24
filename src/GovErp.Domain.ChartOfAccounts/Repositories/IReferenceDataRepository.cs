using GovErp.Domain.ChartOfAccounts.Entities;

namespace GovErp.Domain.ChartOfAccounts.Repositories;

/// <summary>
/// Reference lists of departments and object codes.
/// </summary>
public interface IReferenceDataRepository
{
    Task<Department?> FindDepartmentAsync(DepartmentCode code, CancellationToken ct = default);
    Task<ObjectCodeDefinition?> FindObjectAsync(ObjectCode code, CancellationToken ct = default);
    Task<IReadOnlyList<Department>> ListDepartmentsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ObjectCodeDefinition>> ListObjectsAsync(CancellationToken ct = default);
}
