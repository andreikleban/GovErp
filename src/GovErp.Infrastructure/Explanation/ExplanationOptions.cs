namespace GovErp.Infrastructure.Explanation;

public sealed class ExplanationOptions
{
    public string Provider { get; set; } = ExplanationConfiguration.Template;
    public string Model { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 10;
    public int MaxOutputCharacters { get; set; } = 6000;
}
