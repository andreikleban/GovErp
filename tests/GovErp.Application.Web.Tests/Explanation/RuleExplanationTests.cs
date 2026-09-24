using GovErp.Application.Web.Explanation;
using GovErp.Application.Web.Reference;
using GovErp.Application.Web.Reference.Contracts;
using GovErp.Domain.Validation.DomainServices;
using GovErp.Infrastructure.Explanation;
using GovErp.Infrastructure.Seed;
using Microsoft.Extensions.AI;

namespace GovErp.Application.Web.Tests.Explanation;

public class RuleExplanationTests
{
    private static readonly RuleDescriptionVm Procurement = RuleDescriptions.Find("PROCUREMENT_THRESHOLD")!;

    private static readonly RuleVm Current = new(Guid.NewGuid(), "PROCUREMENT_THRESHOLD", 1, 4, "State", null, null, "SoftStop",
        new Dictionary<string, string> { ["threshold"] = "25000" }, ["FinanceDirector"], new DateOnly(2025, 7, 1), null, true, "m", true);

    [Fact]
    public void Every_rule_of_the_engine_catalog_is_described_with_the_parameters_it_really_has()
    {
        var seeded = RuleSeed.All(new DateOnly(2025, 7, 1)).ToDictionary(r => r.RuleId);
        foreach (var ruleId in RuleCatalog.Default.MandatoryRuleIds)
        {
            var description = RuleDescriptions.Find(ruleId);
            description.Should().NotBeNull($"{ruleId} is enforced by the engine and needs a description");
            description!.Step.Should().Be((int)seeded[ruleId].Step, ruleId);
            description.Parameters.Keys.Should().BeEquivalentTo(seeded[ruleId].Parameters.Keys, ruleId);
            description.SmeQuestions.Should().NotBeEmpty(ruleId);
        }

        RuleDescriptions.All.Select(d => d.RuleId).Should().BeEquivalentTo(RuleCatalog.Default.MandatoryRuleIds);
    }

    [Fact]
    public async Task Template_mode_retells_the_description_and_hides_expert_notes_from_the_public()
    {
        var generator = new LlmRuleExplanationGenerator(new ExplanationOptions());

        var auditor = await generator.ExplainAsync(Procurement, Current, ExplanationAudience.Auditor);
        var citizen = await generator.ExplainAsync(Procurement, Current, ExplanationAudience.Public);

        auditor.Provider.Should().Be("Template");
        auditor.Text.Should().Contain("threshold = 25000").And.Contain("Demo assumption").And.Contain("expert");
        citizen.Text.Should().NotContain("expert").And.NotContain("threshold = ");
    }

    [Fact]
    public async Task Model_answer_is_returned_and_a_failure_falls_back_to_the_written_description()
    {
        var options = new ExplanationOptions { Provider = "OpenAI", TimeoutSeconds = 10, MaxOutputCharacters = 6000 };
        var ok = new FakeChatClient(_ => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Plain words."))));
        var broken = new FakeChatClient(_ => throw new HttpRequestException("down"));

        var answered = await new LlmRuleExplanationGenerator(options, ok).ExplainAsync(Procurement, Current, ExplanationAudience.FinanceUser);
        var fallback = await new LlmRuleExplanationGenerator(options, broken).ExplainAsync(Procurement, Current, ExplanationAudience.FinanceUser);

        answered.Text.Should().Be("Plain words.");
        answered.PromptVersion.Should().Be(RuleExplanationPrompt.Version);
        ok.LastMessages!.Last().Text.Should().Contain("\"threshold\"").And.Contain("25000");   // the model gets the description and the current values
        fallback.Provider.Should().Be("Template");
        fallback.FallbackReason.Should().Be("OpenAI request failed.");
        fallback.Text.Should().Contain(Procurement.Title);
    }

    private sealed class FakeChatClient(Func<CancellationToken, Task<ChatResponse>> respond) : IChatClient
    {
        public IReadOnlyList<ChatMessage>? LastMessages { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            LastMessages = messages.ToList();
            return respond(cancellationToken);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
        }
    }
}
