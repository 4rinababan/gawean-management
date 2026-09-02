using Microsoft.EntityFrameworkCore;
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
                    await QueueMentionsAsync(db, queued, change.NewValue ?? "", reference, issue, actorUserId, actorName, issueUrl, ct);
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
        IAppDbContext db, Dictionary<string, Notification> queued, string body, string reference, Issue issue,
        string actorUserId, string actorName, string url, CancellationToken ct)
    {
        var tokens = Mentions.Extract(body);
        if (tokens.Count == 0)
            return;

        // Identity's UserName is the email address, so "@ari" would never match it. Resolve mentions
        // against this organization's members by display name, email local part or username instead.
        var memberIds = await db.OrganizationMembers
            .Where(m => m.OrganizationId == issue.OrganizationId)
            .Select(m => m.UserId)
            .ToListAsync(ct);
        var members = await users.GetManyAsync(memberIds, ct);

        foreach (var token in tokens)
        {
            var match = members.Values.FirstOrDefault(u => Mentions.Matches(u, token));
            if (match is null || match.Id == actorUserId)
                continue;

            queued[match.Id] = new Notification(
                issue.OrganizationId, match.Id, NotificationType.IssueMentioned,
                $"{actorName} mentioned you in {reference}", issue.Id, url);
        }
    }

}
