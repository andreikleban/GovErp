namespace GovErp.Domain.ChartOfAccounts.Entities;

/// <summary>Справочная запись object-кода: вид расхода / актива / обязательства / бюджетный счёт.</summary>
public sealed class ObjectCodeDefinition
{
    public ObjectCode Code { get; private set; }
    public string Name { get; private set; }
    public ObjectCategory Category { get; private set; }
    public bool IsActive { get; private set; }

    public ObjectCodeDefinition(ObjectCode code, string name, ObjectCategory category, bool isActive = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(code);
        Code = code;
        Name = name;
        Category = category;
        IsActive = isActive;
    }

    private ObjectCodeDefinition() { Code = null!; Name = null!; }
}
