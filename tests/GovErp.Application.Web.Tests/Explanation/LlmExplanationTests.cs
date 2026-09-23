using GovErp.Application.Web.Explanation;
using GovErp.Application.Web.Tests;
using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.ValueObjects;
using GovErp.Infrastructure.Explanation;
using GovErp.Infrastructure.Seed;
using Microsoft.Extensions.AI;

namespace GovErp.Application.Web.Tests.Explanation;

public class LlmExplanationTests
{
    [Fact]
    public async Task Successful_response_keeps_provenance_and_sends_no_tools()
    {
        var fake = new FakeChatClient
        {
            Respond = _ => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "The invoice is stopped."))),
        };
        var result = await Generator(OpenAi(), fake).ExplainAsync(await ExerciseRecord(), ExplanationAudience.Auditor);

        result.Text.Should().Be("The invoice is stopped.");
        result.Provider.Should().Be("OpenAI");
        result.Model.Should().Be("gpt-5.1");
        result.PromptVersion.Should().Be(ExplanationPrompt.Version);
        result.FallbackReason.Should().BeNull();
        fake.Calls.Should().Be(1);
        fake.LastOptions!.ToolMode.Should().Be(ChatToolMode.None);
        fake.LastOptions.Tools.Should().BeNull();
        var prompt = string.Join('\n', fake.LastMessages!.Select(message => message.Text));
        prompt.Should().Contain(ExplanationPrompt.Instruction).And.Contain("13,000.00").And.Contain("BUDGET_AVAILABILITY");
    }

    [Fact]
    public async Task Public_prompt_omits_user_ids_and_raw_inputs()
    {
        var fake = new FakeChatClient { Respond = _ => Task.FromResult(Reply("ok")) };
        await Generator(OpenAi(), fake).ExplainAsync(await ExerciseRecord(), ExplanationAudience.Public);

        var prompt = string.Join('\n', fake.LastMessages!.Select(message => message.Text));
        prompt.Should().NotContain(SpringfieldData.ClerkId.ToString()).And.NotContain("amended=");
    }

    [Fact]
    public async Task Empty_error_and_overlong_responses_fall_back_to_the_template()
    {
        var record = await ExerciseRecord();
        var empty = await Generator(OpenAi(), new FakeChatClient { Respond = _ => Task.FromResult(Reply("  ")) })
            .ExplainAsync(record, ExplanationAudience.FinanceUser);
        empty.Provider.Should().Be("Template");
        empty.PromptVersion.Should().Be(TemplateExplanationGenerator.TemplateVersion);
        empty.FallbackReason.Should().Be("The model returned an empty explanation.");
        empty.Text.Should().Contain("HARD STOP");

        var failed = await Generator(OpenAi(), new FakeChatClient { Respond = _ => throw new HttpRequestException("down") })
            .ExplainAsync(record, ExplanationAudience.FinanceUser);
        failed.Provider.Should().Be("Template");
        failed.FallbackReason.Should().Be("OpenAI request failed.");
        failed.Text.Should().Contain("HARD STOP");

        var options = OpenAi();
        options.MaxOutputCharacters = 8;
        var overlong = await Generator(options, new FakeChatClient { Respond = _ => Task.FromResult(Reply("123456789")) })
            .ExplainAsync(record, ExplanationAudience.FinanceUser);
        overlong.FallbackReason.Should().Be("The model response exceeded 8 characters.");
        overlong.Provider.Should().Be("Template");
    }

    [Fact]
    public async Task Timeout_falls_back_and_caller_cancellation_propagates()
    {
        var record = await ExerciseRecord();
        var slow = new FakeChatClient
        {
            Respond = async ct =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return Reply("late");
            },
        };
        var timed = OpenAi();
        timed.TimeoutSeconds = 1;
        var timeout = await Generator(timed, slow).ExplainAsync(record, ExplanationAudience.FinanceUser);
        timeout.Provider.Should().Be("Template");
        timeout.FallbackReason.Should().Be("The model did not respond within 1 seconds.");

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = new FakeChatClient
        {
            Respond = async ct =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.Infinite, ct);
                return Reply("late");
            },
        };
        using var caller = new CancellationTokenSource();
        var pending = Generator(OpenAi(), waiting).ExplainAsync(record, ExplanationAudience.FinanceUser, caller.Token);
        await started.Task;
        await caller.CancelAsync();
        var act = () => pending;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Template_mode_does_not_call_a_client()
    {
        var fake = new FakeChatClient { Respond = _ => Task.FromResult(Reply("should not be used")) };
        var result = await Generator(new ExplanationOptions { Provider = "Template" }, fake)
            .ExplainAsync(await ExerciseRecord(), ExplanationAudience.FinanceUser);

        fake.Calls.Should().Be(0);
        result.Provider.Should().Be("Template");
        result.FallbackReason.Should().BeNull();
        result.Text.Should().Contain("HARD STOP");
    }

    [Fact]
    public async Task Missing_client_uses_the_template_and_names_the_reason()
    {
        var result = await new LlmExplanationGenerator(new TemplateExplanationGenerator(), OpenAi())
            .ExplainAsync(await ExerciseRecord(), ExplanationAudience.FinanceUser);

        result.Provider.Should().Be("Template");
        result.FallbackReason.Should().Be("OpenAI API key is not configured.");
    }

    private static ExplanationOptions OpenAi() => new() { Provider = "OpenAI", TimeoutSeconds = 10, MaxOutputCharacters = 6000 };

    private static LlmExplanationGenerator Generator(ExplanationOptions options, IChatClient chat) =>
        new(new TemplateExplanationGenerator(), options, chat);

    private static ChatResponse Reply(string text) => new(new ChatMessage(ChatRole.Assistant, text));

    private static async Task<Domain.Validation.Entities.EvaluationRecord> ExerciseRecord()
    {
        var data = SpringfieldData.Create();
        var subject = await ValidationSubjectAssemblerTests.Assembler(data).BuildAsync(data.NonPoExerciseInvoice(), SpringfieldData.Jun15);
        return new ValidationPipeline(RuleCatalog.Default).Evaluate(subject, data.Rules,
            EvaluationTrigger.Manual, SpringfieldData.ClerkId, new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero));
    }

    private sealed class FakeChatClient : IChatClient
    {
        public int Calls { get; private set; }
        public IReadOnlyList<ChatMessage>? LastMessages { get; private set; }
        public ChatOptions? LastOptions { get; private set; }
        public required Func<CancellationToken, Task<ChatResponse>> Respond { get; init; }

        public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastMessages = messages.ToList();
            LastOptions = options;
            return await Respond(cancellationToken);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
        }
    }
}
