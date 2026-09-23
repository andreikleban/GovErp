namespace GovErp.Infrastructure.Persistence;

/// <summary>Последний выданный номер серии (например, «AP-2026»). Инфраструктурная модель, не домен.</summary>
public sealed class DocumentCounter
{
    public string Series { get; set; } = "";
    public int LastValue { get; set; }
}
