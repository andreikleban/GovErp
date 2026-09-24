namespace GovErp.Infrastructure.Persistence;

/// <summary>The last issued number of a series (for example, "AP-2026"). An infrastructure model, not domain.</summary>
public sealed class DocumentCounter
{
    public string Series { get; set; } = "";
    public int LastValue { get; set; }
}
