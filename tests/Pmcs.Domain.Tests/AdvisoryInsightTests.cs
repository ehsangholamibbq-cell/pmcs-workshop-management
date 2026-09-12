using System.Text.Json;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Intelligence.Domain;
using Pmcs.Modules.Intelligence.Services;

namespace Pmcs.Domain.Tests;

public sealed class AdvisoryInsightTests
{
    [Fact]
    public void ValidOutputCanBeCreatedAndReviewedOnce()
    {
        var generatedAt = TestTime();
        var insight = CreateInsight(generatedAt);

        Assert.Equal(AdvisoryReviewStatus.NeedsReview, insight.ReviewStatus);
        Assert.Equal(1, insight.Revision);
        Assert.Equal("project-state:aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", insight.Output.EvidenceReferences.Single());

        insight.Review(
            AdvisoryReviewStatus.Accepted,
            Guid.Parse("77777777-7777-7777-7777-777777777777"),
            "مورد بررسی شد.",
            baseRevision: 1,
            generatedAt.AddMinutes(10));

        Assert.Equal(AdvisoryReviewStatus.Accepted, insight.ReviewStatus);
        Assert.Equal(2, insight.Revision);
        Assert.Equal("مورد بررسی شد.", insight.ReviewComment);
        Assert.Throws<DomainRuleException>(() => insight.Review(
            AdvisoryReviewStatus.Dismissed,
            Guid.NewGuid(),
            null,
            baseRevision: 2,
            generatedAt.AddMinutes(20)));
    }

    [Fact]
    public void ReviewRejectsStaleRevision()
    {
        var insight = CreateInsight(TestTime());

        var exception = Assert.Throws<DomainRuleException>(() => insight.Review(
            AdvisoryReviewStatus.Accepted,
            Guid.NewGuid(),
            null,
            baseRevision: 0,
            TestTime().AddMinutes(5)));

        Assert.Equal("insight.revision.conflict", exception.Code);
    }

    [Fact]
    public void ValidatorRejectsCitationOutsideContextManifest()
    {
        var exception = Assert.Throws<DomainRuleException>(() =>
            AdvisoryOutputValidator.NormalizeAndValidate(
                ValidOutput() with { EvidenceReferences = ["daily-fact:unknown"] },
                new HashSet<string> { "project-state:known" }));

        Assert.Equal("insight.citation.unsupported", exception.Code);
    }

    [Fact]
    public void ResponsesParserReadsStructuredOutput()
    {
        var output = JsonSerializer.Serialize(ValidOutput(), AdvisoryJson.Options);
        var response = JsonSerializer.SerializeToElement(new
        {
            id = "resp_test",
            status = "completed",
            output = new[]
            {
                new
                {
                    type = "message",
                    content = new[] { new { type = "output_text", text = output } }
                }
            }
        });

        var result = OpenAiResponsesClient.ParseResponse(response, "configured-model");

        Assert.Equal("OpenAI", result.Provider);
        Assert.Equal("configured-model", result.Model);
        Assert.Equal("resp_test", result.ProviderResponseId);
        Assert.Equal("خلاصه مدیریتی مستند", result.Output.Statement);
    }

    [Fact]
    public void ResponsesRequestUsesStrictSchemaAndDisablesStorage()
    {
        var request = JsonSerializer.SerializeToElement(
            OpenAiResponsesClient.BuildRequest("configured-model", "{}"));

        Assert.False(request.GetProperty("store").GetBoolean());
        Assert.Equal("json_schema", request.GetProperty("text").GetProperty("format").GetProperty("type").GetString());
        Assert.True(request.GetProperty("text").GetProperty("format").GetProperty("strict").GetBoolean());
        Assert.False(request.GetProperty("text").GetProperty("format").GetProperty("schema").GetProperty("additionalProperties").GetBoolean());
    }

    [Fact]
    public void ResponsesParserTreatsRefusalAsSafeFailure()
    {
        var response = JsonSerializer.SerializeToElement(new
        {
            id = "resp_refusal",
            status = "completed",
            output = new[]
            {
                new
                {
                    type = "message",
                    content = new[] { new { type = "refusal", refusal = "cannot comply" } }
                }
            }
        });

        var exception = Assert.Throws<AdvisoryModelException>(() =>
            OpenAiResponsesClient.ParseResponse(response, "configured-model"));

        Assert.Equal("ai.provider.refusal", exception.Code);
        Assert.False(exception.IsTransient);
    }

    [Fact]
    public void GenerationRequestHasExplicitTerminalState()
    {
        var startedAt = TestTime();
        var request = InsightGenerationRequest.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), startedAt);

        request.Start(startedAt.AddMinutes(1));
        request.Complete(Guid.NewGuid(), Guid.NewGuid(), startedAt.AddMinutes(2));

        Assert.Equal(InsightGenerationStatus.Succeeded, request.Status);
        Assert.Equal(1, request.Attempts);
        Assert.NotNull(request.CompletedAt);
    }

    private static AdvisoryInsight CreateInsight(DateTimeOffset generatedAt) => AdvisoryInsight.Create(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Guid.Parse("33333333-3333-3333-3333-333333333333"),
        Guid.Parse("44444444-4444-4444-4444-444444444444"),
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        Guid.Parse("66666666-6666-6666-6666-666666666666"),
        ValidOutput(),
        new string('A', 64),
        includesFinancialData: false,
        includesCommercialData: false,
        includesActionData: true,
        "OpenAI",
        "configured-model",
        "resp_test",
        "prompt-v1",
        "policy-v1",
        generatedAt,
        generatedAt.AddHours(24));

    private static AdvisoryInsightOutput ValidOutput() => new(
        AdvisoryInsightType.ExecutiveSummary,
        "خلاصه مدیریتی مستند",
        ["project-state:aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"],
        ["پوشش گزارش‌های تأییدشده محدود است."],
        [],
        ["بودجه مبنای مصوب در دسترس نیست."],
        AdvisoryConfidenceBand.Medium,
        "ممکن است تصمیم‌گیری مالی به داده تکمیلی نیاز داشته باشد.",
        [new AdvisorySuggestedAction("تکمیل داده", "برای کاهش عدم قطعیت")],
        "مدیر پروژه");

    private static DateTimeOffset TestTime() =>
        new(2026, 9, 10, 8, 0, 0, TimeSpan.Zero);
}
