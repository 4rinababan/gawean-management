using System.Text.RegularExpressions;
using TaskManagement.Application.Abstractions;

namespace TaskManagement.Application.Services;

/// <summary>
/// The single definition of what an <c>@mention</c> is. The comment box offers handles from
/// <see cref="HandleFor"/> and the notifier resolves them with <see cref="Matches"/>, so what the
/// picker inserts is always something the matcher accepts — no guessing at someone's handle.
/// </summary>
public static partial class Mentions
{
    /// <summary>The @tokens in a comment body. An email address is not a mention.</summary>
    public static IReadOnlyList<string> Extract(string body)
        => TokenPattern().Matches(body)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Positions of every @token in <paramref name="body"/>, for rendering (e.g. highlighting mentions in
    /// a comment). Kept separate from <see cref="Extract"/> because a renderer needs where each token is,
    /// not just its distinct set.
    /// </summary>
    public static IEnumerable<(int Start, int Length, string Token)> FindTokens(string body)
        => TokenPattern().Matches(body).Select(m => (m.Index, m.Length, m.Groups[1].Value));

    /// <summary>The handle to insert for a person: their display name with separators removed.</summary>
    public static string HandleFor(string displayName, string email)
    {
        var fromName = Normalize(displayName);
        return fromName.Length > 0 ? fromName : Normalize(email.Split('@')[0]);
    }

    /// <summary>A token matches a person by handle, email local part or display name — all separator-insensitive.</summary>
    public static bool Matches(UserSummary user, string token)
    {
        var normalized = Normalize(token);
        if (normalized.Length == 0)
            return false;

        return Normalize(user.UserName) == normalized
            || Normalize(user.Email.Split('@')[0]) == normalized
            || Normalize(user.DisplayName) == normalized;
    }

    /// <summary>Lowercase, alphanumerics only — so "Bang Boy", "bang.boy" and "bangboy" are one handle.</summary>
    private static string Normalize(string? value)
        => new((value ?? "").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    [GeneratedRegex(@"(?<![\w@])@([A-Za-z0-9_.-]{2,64})")]
    private static partial Regex TokenPattern();
}
