namespace TaskManagement.Infrastructure.Ai;

/// <summary>
/// Connection settings for an OpenAI-compatible chat-completions endpoint. Left empty by default:
/// with no <see cref="ApiKey"/> the assistant reports itself disabled and the app behaves as if the
/// feature did not exist. The key belongs in user-secrets (dev) or the environment (prod), never in
/// appsettings.json — that file is committed.
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>Base URL including the version segment, e.g. https://api.biznetgio.ai/v1</summary>
    public string Endpoint { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Reasoning models spend part of the completion budget thinking before they emit any content,
    /// so this has to be generous: too low and the response is all reasoning and no answer.
    /// </summary>
    public int MaxTokens { get; set; } = 1500;

    /// <summary>Low by default — drafting a ticket rewards consistency over invention.</summary>
    public double Temperature { get; set; } = 0.3;

    /// <summary>Model calls are slow; this bounds how long a user can be left waiting.</summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Per-user cap on AI calls per rolling hour — the endpoint is a paid external API.</summary>
    public int RateLimitPerHour { get; set; } = 30;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint)
        && !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(Model);
}
