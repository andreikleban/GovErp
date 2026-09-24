namespace GovErp.Infrastructure.Master;

/// <summary>
/// A user account in the master database. An anemic infrastructure model, not domain (DDD-7).
/// </summary>
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
