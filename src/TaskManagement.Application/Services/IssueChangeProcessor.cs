using TaskManagement.Application.Abstractions;
using TaskManagement.Domain;
using TaskManagement.Domain.Issues;
using TaskManagement.Domain.Notifications;

namespace TaskManagement.Application.Services;

/// <summary>
/// Turns the field-level <see cref="IssueChange"/> records emitted by the <see cref="Issue"/> aggregate into
/// persisted <see cref="ActivityLog"/> rows plus in-app / email notifications. Called once per mutating operation,
/// after the issue has been mutated but before <c>SaveChangesAsync</c>.
/// </summary>
public sealed class IssueChangeProcessor(
    IUserDirectory users,
    IEmailSender email,
    INotificationRealtime realtime,
    ITenantContext tenant,
    IAppUrls urls)
{
    /// <summary>Consumes the aggregate's queued changes on the caller's unit of work, then commits it.</summary>
    public async Task ProcessAsync(IAppDbContext db, Issue issue, string projectKey, string actorUserId, CancellationToken ct = default)
    {
        var changes = issue.DequeueChanges();
        if (changes.Count == 0)
        {
            // No field-level changes to log, but the caller may still have pending inserts/updates.
            await db.SaveChangesAsync(ct);
            return;
        }

        var reference = $"{projectKey}-{issue.Number}";
        var issueUrl = urls.Issue(tenant.Slug, issue.Id);
        var actorName = (await users.GetAsync(actorUserId, ct))?.DisplayName ?? "Someone";

        // recipient -> the single most relevant notification for this operation
        var queued = new Dictionary<string, Notification>();

        void Queue(string? userId, NotificationType type, string message)
        {
            if (string.IsNullOrEmpty(userId) || userId == actorUserId)
                return;
            queued[userId] = new Notification(issue.OrganizationId, userId, type, message, issue.Id, issueUrl);
        }

        void QueueStakeholders(NotificationType type, string message)
        {
            Queue(issue.AssigneeUserId, type, message);
            Queue(issue.ReporterUserId, type, message);
        }

        foreach (var change in changes)
        {
            db.ActivityLogs.Add(new ActivityLog(
                issue.OrganizationId, issue.Id, change.ActorUserId, change.Field, change.OldValue, change.NewValue));

            switch (change.Field)
            {
                case nameof(Issue.AssigneeUserId) when !string.IsNullOrEmpty(change.NewValue):
                    Queue(change.NewValue, NotificationType.IssueAssigned,
                        $"{actorName} assigned {reference} to you: {issue.Title}");
                    await SendAssignmentEmailAsync(change.NewValue!, reference, issue.Title, issueUrl, actorName, ct);
                    break;

                case nameof(Issue.Status):
                    QueueStakeholders(NotificationType.IssueStatusChanged,
                        $"{actorName} moved {reference} to {change.NewValue}");
                    break;

                case nameof(Comment):
                    QueueStakeholders(NotificationType.IssueCommented, $"{actorName} commented on {reference}");
                    await QueueMentionsAsync(queued, change.NewValue ?? "", reference, issue, actorUserId, actorName, issueUrl, ct);
                    break;
            }
        }

        foreach (var notification in queued.Values)
            db.Notifications.Add(notification);

        await db.SaveChangesAsync(ct);

        foreach (var recipient in queued.Keys)
            await realtime.NotifyAsync(recipient, ct);
    }

    private async Task SendAssignmentEmailAsync(string userId, string reference, string title, string url, string actorName, CancellationToken ct)
    {
        var user = await users.GetAsync(userId, ct);
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
            return;

        await email.SendAsync(
            user.Email,
            $"[{reference}] assigned to you",
            $"<p>{actorName} assigned <a href=\"{url}\">{reference} — {title}</a> to you.</p>",
            ct);
    }

    private async Task QueueMentionsAsync(
        Dictionary<string, Notification> queued, string body, string reference, Issue issue,
        string actorUserId, string actorName, string url, CancellationToken ct)
    {
        foreach (var username in ExtractMentions(body))
        {
            var user = await users.FindByUsernameAsync(username, ct);
            if (user is null || user.Id == actorUserId)
                continue;

            queued[user.Id] = new Notification(
                issue.OrganizationId, user.Id, NotificationType.IssueMentioned,
                $"{actorName} mentioned you in {reference}", issue.Id, url);
        }
    }

    private static IEnumerable<string> ExtractMentions(string body)
        => System.Text.RegularExpressions.Regex
            .Matches(body, @"(?<![\w])@([A-Za-z0-9_.-]{2,64})")
            .Select(m => m.Groups[1].Value.ToLowerInvariant())
            .Distinct();
}
