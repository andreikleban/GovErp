namespace GovErp.Infrastructure.Master;

/// <summary>Учётная запись пользователя в master-БД. Анемичная инфраструктурная модель, не домен (DDD-7).</summary>
public sealed class UserAccount
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string TenantId { get; set; } = "";
    public List<string> Roles { get; set; } = [];
    public string? DepartmentCode { get; set; }
}
