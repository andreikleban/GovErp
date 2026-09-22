using GovErp.Application.Web.Explanation;
using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.ValueObjects;
using GovErp.Infrastructure.Explanation;
using GovErp.Infrastructure.Seed;

namespace GovErp.Application.Web.Tests;

public class TemplateExplanationTests
{
    private static async Task<Domain.Validation.Entities.EvaluationRecord> ExerciseRecord()
    {
        var d = SpringfieldData.Create();
        var subject = await ValidationSubjectAssemblerTests.Assembler(d).BuildAsync(d.NonPoExerciseInvoice(), SpringfieldData.Jun15);
        return new ValidationPipeline(RuleCatalog.Default).Evaluate(subject, d.Rules,
            EvaluationTrigger.Manual, SpringfieldData.ClerkId, new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Auditor_text_explains_decision_funds_checks_and_versions()
    {
        var result = await new TemplateExplanationGenerator().ExplainAsync(await ExerciseRecord(), ExplanationAudience.Auditor);
        result.Provider.Should().Be("Template");
        result.PromptVersion.Should().Be("template-1");
        result.Text.Should().Contain("HARD STOP").And.Contain("13,000.00").And.Contain("701-6000-53100-G-COPS-26")
            .And.Contain("BUDGET_AVAILABILITY").And.Contain("engine-1.0.0").And.Contain("Budget amendment");
    }

    [Fact]
    public async Task Public_text_has_no_user_ids_or_raw_inputs()
    {
        var text = (await new TemplateExplanationGenerator().ExplainAsync(await ExerciseRecord(), ExplanationAudience.Public)).Text;
        text.Should().NotContain(SpringfieldData.ClerkId.ToString()).And.NotContain("amended=");
        text.Should().Contain("HARD STOP");
    }
}
