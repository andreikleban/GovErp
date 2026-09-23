namespace GovErp.Application.Web.Tenancy;

/// <summary>
/// Разрешение UserId → отображаемое имя для Audit (оценки, события) и для панели аудита в карточке инвойса —
/// одна и та же логика переиспользуется везде (spec §6). Тенант демо-масштаба (десяток пользователей), поэтому
/// один ListAsync на список — дёшево; для оценок/событий по одному документу это не хуже прежнего поведения.
/// </summary>
public static class TenantUserNaming
{
    public static async Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(this ITenantUserDirectory directory, TenantId tenantId,
        CancellationToken ct = default) =>
        (await directory.ListAsync(tenantId, ct)).ToDictionary(u => u.Id, u => u.DisplayName);

    /// <summary>Неизвестный (например, удалённый) пользователь — сырой id как заглушка, как и для ссылок на инвойсы.</summary>
    public static string NameOf(this IReadOnlyDictionary<Guid, string> names, Guid userId) =>
        names.GetValueOrDefault(userId, userId.ToString());
}
