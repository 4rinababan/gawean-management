using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaskManagement.Application.Abstractions;
using TaskManagement.Domain;
using TaskManagement.Infrastructure.Ai;
using TaskManagement.Infrastructure.Content;

namespace TaskManagement.Infrastructure.Tests;

/// <summary>
/// The assistant reads a reply written by a model, so every case here is a shape the endpoint has
/// actually been observed to return — including the ones that are not a well-formed draft. A model
/// that misbehaves must produce a message the author can act on, never a crash or silent bad data.
/// </summary>
public class AiAssistantTests
{
    private static ChatAiAssistant Build(HttpStatusCode status, string body)
    {
        var http = new HttpClient(new StubHandler(status, body))
        {
            BaseAddress = new Uri("https://model.example/v1/"),
        };

        return new ChatAiAssistant(
            http,
            Options.Create(new AiOptions
            {
                Endpoint = "https://model.example/v1",
                ApiKey = "test-key",
                Model = "test-model",
            }),
            new RichTextSanitizer(),
            NullLogger<ChatAiAssistant>.Instance);
    }

    private static string Completion(string content, string finishReason = "stop")
    {
        var encoded = System.Text.Json.JsonSerializer.Serialize(content);
        return $"{{\"choices\":[{{\"finish_reason\":\"{finishReason}\",\"message\":{{\"role\":\"assistant\",\"content\":{encoded}}}}}]}}";
    }

    [Fact]
    public async Task A_well_formed_reply_becomes_a_draft()
    {
        var assistant = Build(HttpStatusCode.OK, Completion(
            """{"title":"Upload fails over 10MB in Safari","description":"<p>Expected the file to upload.</p>","type":"Bug","priority":"High","storyPoints":3}"""));

        var draft = await assistant.DraftIssueAsync("upload gagal di safari");

        draft.Title.Should().Be("Upload fails over 10MB in Safari");
        draft.Description.Should().Contain("Expected the file to upload.");
        draft.Type.Should().Be(IssueType.Bug);
        draft.Priority.Should().Be(IssuePriority.High);
        draft.StoryPoints.Should().Be(3);
    }

    [Fact]
    public async Task Script_in_the_models_description_is_stripped()
    {
        // The model is an untrusted source like any other. This is the boundary that makes it safe
        // to drop its output straight into a description field.
        var assistant = Build(HttpStatusCode.OK, Completion(
            """{"title":"x","description":"<p>ok</p><script>alert(1)</script>","type":"Task","priority":"Medium"}"""));

        var draft = await assistant.DraftIssueAsync("anything");

        draft.Description.Should().Contain("ok").And.NotContain("<script");
    }

    [Fact]
    public async Task A_reply_wrapped_in_a_markdown_fence_is_still_read()
    {
        var assistant = Build(HttpStatusCode.OK, Completion(
            "```json\n{\"title\":\"Fenced\",\"type\":\"Story\",\"priority\":\"Low\"}\n```"));

        (await assistant.DraftIssueAsync("x")).Title.Should().Be("Fenced");
    }

    [Fact]
    public async Task Running_out_of_tokens_mid_reasoning_explains_itself()
    {
        // Observed against the live endpoint: a reasoning model can spend the whole budget thinking
        // and return a message with no content at all.
        var assistant = Build(HttpStatusCode.OK,
            """{"choices":[{"finish_reason":"length","message":{"role":"assistant","reasoning_content":"still thinking"}}]}""");

        var act = () => assistant.DraftIssueAsync("x");

        (await act.Should().ThrowAsync<AiAssistantException>()).Which.Message.Should().Contain("ran out of room");
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("""{"title":"","type":"Bug"}""")]
    public async Task An_unusable_reply_raises_a_readable_error(string content)
    {
        var assistant = Build(HttpStatusCode.OK, Completion(content));

        await assistant.Invoking(a => a.DraftIssueAsync("x")).Should().ThrowAsync<AiAssistantException>();
    }

    [Fact]
    public async Task An_http_failure_does_not_leak_the_body_to_the_user()
    {
        var assistant = Build(HttpStatusCode.Unauthorized, """{"error":{"message":"bad key sk-secret"}}""");

        var act = () => assistant.DraftIssueAsync("x");

        (await act.Should().ThrowAsync<AiAssistantException>()).Which.Message.Should().NotContain("sk-secret");
    }

    [Fact]
    public async Task Unknown_enum_values_fall_back_rather_than_failing_the_draft()
    {
        var assistant = Build(HttpStatusCode.OK, Completion(
            """{"title":"x","type":"Improvement","priority":"Urgent","storyPoints":-4}"""));

        var draft = await assistant.DraftIssueAsync("x");

        draft.Type.Should().Be(IssueType.Task);
        draft.Priority.Should().Be(IssuePriority.Medium);
        draft.StoryPoints.Should().BeNull();
    }

    [Fact]
    public async Task An_empty_prompt_never_reaches_the_model()
    {
        var assistant = Build(HttpStatusCode.OK, Completion("{}"));

        await assistant.Invoking(a => a.DraftIssueAsync("   ")).Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void The_disabled_assistant_reports_itself_so_the_ui_can_hide_the_feature()
        => new DisabledAiAssistant().IsEnabled.Should().BeFalse();

    [Theory]
    [InlineData("", false)]
    [InlineData("https://x/v1", false)]
    public void Options_without_a_key_are_not_configured(string endpoint, bool expected)
        => new AiOptions { Endpoint = endpoint, Model = "m" }.IsConfigured.Should().Be(expected);

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }
}
