namespace GovErp.Application.Web.Reference.Contracts;

/// <summary>
/// The written rule description: what it checks, on which facts, when it fires and how it is resolved,
/// where the rule comes from and what needs expert confirmation. The source of truth for the Description dialog and for the LLM.
/// </summary>
public sealed record RuleDescriptionVm(string RuleId, int Step, string StepName, string Title, string Checks,
    IReadOnlyList<string> Facts, IReadOnlyDictionary<string, string> Parameters, string WhenFired, string Resolution,
    string Source, IReadOnlyList<string> SmeQuestions);
