namespace GovErp.Application.Web.Tenancy;

/// <summary>
/// Resolves UserId → display name for Audit (evaluations, events) and for the audit panel in the invoice card;
/// the same logic is reused everywhere (spec §6). The tenant is demo-sized (a dozen users), so
/// one ListAsync per list is cheap; for the evaluations/events of a single document it is no worse than before.
/// </summary>
public static class TenantUserNaming
{
    public static async Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(this ITenantUserDirectory directory, TenantId tenantId,
        CancellationToken ct = default) =>
        (await directory.ListAsync(tenantId, ct)).ToDictionary(u => u.Id, u => u.DisplayName);

    /// <summary>An unknown (for example, deleted) user falls back to the raw id, as for invoice links.</summary>
    public static string NameOf(this IReadOnlyDictionary<Guid, string> names, Guid userId) =>
        names.GetValueOrDefault(userId, userId.ToString());
}
