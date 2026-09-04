using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Contracts;
using TaskManagement.Domain;

namespace TaskManagement.Infrastructure.Ai;

/// <summary>
/// Talks to any OpenAI-compatible /chat/completions endpoint. Written against that wire format rather
/// than a vendor SDK so the deployment can point at a different provider by changing configuration.
/// </summary>
public sealed class ChatAiAssistant : IAiAssistant
{
    // Asking for HTML directly (rather than markdown we would then convert) keeps the output in the
    // one shape the description field stores, and the allow-list mirrors what the sanitiser permits.
    private const string SystemPrompt = """
        You help a software team write issue tickets. Reply with ONLY a JSON object — no prose,
        no explanation, no markdown code fence.

        LANGUAGE: write title and description in the SAME language the author wrote in. If the
        author wrote Indonesian, the whole ticket must be in Indonesian. Only the JSON keys and
        the type/priority values stay in English.

        Keys:
          title        string, one line, under 120 characters, imperative and specific
          description  string, HTML, using only <p> <ul> <ol> <li> <strong> <em> <code> <pre> tags.
                       For a bug include what was expected and what happened instead; for work items
                       include acceptance criteria as a list. Never invent facts the author did not
                       give — describe only what was reported.
          type         one of: Task, Story, Bug, Epic, SubTask
          priority     one of: Lowest, Low, Medium, High, Highest
          storyPoints  integer from the Fibonacci set 1,2,3,5,8,13 — or null when unclear
        """;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly AiOptions _options;
    private readonly IHtmlSanitizer _sanitizer;
    private readonly ILogger<ChatAiAssistant> _logger;

    public ChatAiAssistant(
        HttpClient http,
        IOptions<AiOptions> options,
        IHtmlSanitizer sanitizer,
        ILogger<ChatAiAssistant> logger)
    {
        _http = http;
        _options = options.Value;
        _sanitizer = sanitizer;
        _logger = logger;
    }

    public bool IsEnabled => _options.IsConfigured;

    public async Task<IssueDraft> DraftIssueAsync(string prompt, string? documentContext = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(prompt) && string.IsNullOrWhiteSpace(documentContext))
        {
            throw new ArgumentException("Describe the issue first.", nameof(prompt));
        }

        if (!IsEnabled)
        {
            throw new InvalidOperationException("No AI model is configured.");
        }

        var userMessage = string.IsNullOrWhiteSpace(documentContext)
            ? prompt.Trim()
            : $"{prompt.Trim()}\n\n--- Content extracted from an attached spec file ---\n{documentContext}";

        var request = new ChatRequest(
            _options.Model,
            [new ChatMessage("system", SystemPrompt), new ChatMessage("user", userMessage)],
            _options.MaxTokens,
            _options.Temperature);

        using var response = await _http.PostAsJsonAsync("chat/completions", request, Json, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("AI request failed with {Status}: {Body}", (int)response.StatusCode, body);
            throw new AiAssistantException($"The model rejected the request ({(int)response.StatusCode}).");
        }

        var completion = await response.Content.ReadFromJsonAsync<ChatResponse>(Json, ct)
            ?? throw new AiAssistantException("The model returned an empty response.");

        var choice = completion.Choices?.FirstOrDefault()
            ?? throw new AiAssistantException("The model returned no choices.");

        // Reasoning models emit their chain of thought in a separate field and only then the answer.
        // If the token budget runs out mid-thought there is no content at all — a real, reproducible
        // outcome that must read as a clear message rather than a null-reference crash.
        if (string.IsNullOrWhiteSpace(choice.Message?.Content))
        {
            _logger.LogWarning("AI returned no content (finish_reason: {Reason}).", choice.FinishReason);
            throw new AiAssistantException(
                choice.FinishReason == "length"
                    ? "The model ran out of room before answering. Try a shorter description."
                    : "The model did not return a draft. Try again.");
        }

        return Parse(choice.Message.Content);
    }

    private IssueDraft Parse(string content)
    {
        var json = ExtractJson(content);

        DraftPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<DraftPayload>(json, Json);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "AI returned unparseable JSON: {Content}", content);
            throw new AiAssistantException("The model's reply could not be read as a draft. Try again.");
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.Title))
        {
            throw new AiAssistantException("The model's reply had no title. Try again.");
        }

        return new IssueDraft(
            payload.Title.Trim(),
            // Model output is untrusted input like anything else a user pastes in.
            _sanitizer.Sanitize(payload.Description),
            ParseEnum(payload.Type, IssueType.Task),
            ParseEnum(payload.Priority, IssuePriority.Medium),
            payload.StoryPoints is > 0 and <= 100 ? payload.StoryPoints : null);
    }

    /// <summary>
    /// Models are told not to fence the JSON, and usually comply — but "usually" is not a contract,
    /// so pull out the outermost object rather than trusting the whole string to parse.
    /// </summary>
    internal static string ExtractJson(string content)
    {
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        return start >= 0 && end > start ? content[start..(end + 1)] : content.Trim();
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : fallback;

    private sealed record ChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("temperature")] double Temperature);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<ChatChoice>? Choices);

    private sealed record ChatChoice(
        [property: JsonPropertyName("message")] ChatResponseMessage? Message,
        [property: JsonPropertyName("finish_reason")] string? FinishReason);

    private sealed record ChatResponseMessage(
        [property: JsonPropertyName("content")] string? Content);

    private sealed record DraftPayload(
        string? Title,
        string? Description,
        string? Type,
        string? Priority,
        int? StoryPoints);
}

/// <summary>A model failure the user can act on, phrased for display in a toast.</summary>
public sealed class AiAssistantException(string message) : Exception(message);

/// <summary>Stands in when no model is configured, so nothing has to null-check the assistant.</summary>
public sealed class DisabledAiAssistant : IAiAssistant
{
    public bool IsEnabled => false;

    public Task<IssueDraft> DraftIssueAsync(string prompt, string? documentContext = null, CancellationToken ct = default) =>
        throw new InvalidOperationException("No AI model is configured.");
}
