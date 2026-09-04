using TaskManagement.Application.Contracts;
using TaskManagement.Domain;

namespace TaskManagement.Application.Abstractions;

/// <summary>The currently signed-in user, resolved from the Blazor authentication state.</summary>
public interface ICurrentUser
{
    /// <summary>Identity user id, or null when unauthenticated.</summary>
    string? UserId { get; }

    string? Email { get; }

    bool IsAuthenticated => UserId is not null;

    string RequireUserId() => UserId ?? throw new InvalidOperationException("No authenticated user in scope.");
}

/// <summary>The organization the current request/circuit is scoped to, plus the caller's role in it.</summary>
public interface ITenantContext
{
    Guid OrganizationId { get; }

    string Slug { get; }

    OrgRole Role { get; }

    bool IsResolved { get; }

    /// <summary>Binds the ambient tenant for the current scope. Called by the routing layer after verifying membership.</summary>
    void Set(Guid organizationId, string slug, OrgRole role);
}

/// <summary>Looks up display information for Identity users referenced by domain entities.</summary>
public interface IUserDirectory
{
    Task<IReadOnlyDictionary<string, UserSummary>> GetManyAsync(IEnumerable<string> userIds, CancellationToken ct = default);

    Task<UserSummary?> GetAsync(string userId, CancellationToken ct = default);

    Task<UserSummary?> FindByEmailAsync(string email, CancellationToken ct = default);

    Task<UserSummary?> FindByUsernameAsync(string username, CancellationToken ct = default);
}

public sealed record UserSummary(string Id, string DisplayName, string Email, string? UserName, string AvatarColor);

/// <summary>Transactional email. Backed by SMTP (MailKit) in production, a no-op/logger in development.</summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);
}

/// <summary>Blob storage for issue attachments. Local disk in the default deployment; swappable for S3/MinIO.</summary>
public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken ct = default);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default);

    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}

/// <summary>Pushes a freshly created notification to any live circuits for the recipient (SignalR).</summary>
public interface INotificationRealtime
{
    Task NotifyAsync(string recipientUserId, CancellationToken ct = default);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>
/// Strips anything unsafe from user-authored HTML (issue descriptions). Applied on write, so what is
/// stored is already safe to render — nothing else in the system may persist raw HTML from a user.
/// </summary>
public interface IHtmlSanitizer
{
    string? Sanitize(string? html);
}

/// <summary>
/// Drafting help from a language model. Optional by design: when no model is configured the
/// application runs exactly as before, so nothing here may become load-bearing for a core flow.
/// </summary>
public interface IAiAssistant
{
    /// <summary>False when no model is configured, so the UI can hide the entry points entirely.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Expands a one-line summary (optionally alongside text extracted from an uploaded spec file) into a
    /// draft ticket. The returned description is already sanitised; everything else is a suggestion the
    /// author is expected to review before saving.
    /// </summary>
    Task<IssueDraft> DraftIssueAsync(string prompt, string? documentContext = null, CancellationToken ct = default);
}

/// <summary>
/// Pulls plain text out of an uploaded spec file so it can be handed to <see cref="IAiAssistant"/> as
/// extra drafting context. Cell/paragraph text only — content drawn as shapes (e.g. an Excel flowchart
/// made of text boxes) isn't captured.
/// </summary>
public interface IDocumentTextExtractor
{
    /// <summary>Supported extensions: .xlsx, .pdf, .docx. Throws for anything else.</summary>
    Task<string> ExtractAsync(Stream content, string fileName, CancellationToken ct = default);
}
