namespace GovErp.Infrastructure.Master;

/// <summary>Запись каталога тенантов. Анемичная инфраструктурная модель, не домен (DDD-7).</summary>
public sealed class Tenant
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string DatabaseName { get; set; } = "";
    /// <summary>Ключ секции конфигурации с учётными данными runtime-пользователя; сам секрет в БД не хранится.</summary>
    public string CredentialKey { get; set; } = "";
    public bool IsDemo { get; set; }
}
