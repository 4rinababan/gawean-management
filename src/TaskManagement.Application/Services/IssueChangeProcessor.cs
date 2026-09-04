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
    IAppUrls urls,
    AutomationEngine automation)
{
    /// <summary>
    /// Consumes the aggregate's queued changes on the caller's unit of work, then commits it. Also runs
    /// automation rules for the issue's project against those same changes (unless this call is itself
    /// processing an automation-caused edit — see <see cref="AutomationEngine.MatchAndApplyAsync"/> for why
    /// that alone is enough to prevent rules from cascading into each other). Pass <paramref name="created"/>
    /// when this is a brand-new issue: a bare-title create queues no field changes at all, so "issue
    /// created" automation triggers need this explicit signal rather than a diff match.
    /// </summary>
    public async Task ProcessAsync(
        IAppDbContext db, Issue issue, string projectKey, string actorUserId,
        CancellationToken ct = default, bool created = false)
    {
        var changes = issue.DequeueChanges();
        var queued = new Dictionary<string, Notification>();

        if (changes.Count > 0)
            await RecordAsync(db, issue, projectKey, actorUserId, changes, queued, ct);

        var fired = await automation.MatchAndApplyAsync(db, issue, changes, created, queued, ct);
        foreach (var (rule, ruleChanges) in fired)
            await RecordAsync(db, issue, projectKey, rule.CreatedByUserId, ruleChanges, queued, ct);

        foreach (var notification in queued.Values)
            db.Notifications.Add(notification);

        await db.SaveChangesAsync(ct);

        foreach (var recipient in queued.Keys)
            await realtime.NotifyAsync(recipient, ct);
    }

    /// <summary>Writes one <see cref="ActivityLog"/> row per change and queues the notifications it implies.
    /// Called once for the changes a real edit produced, and again (with the rule's creator as actor) for
    /// each automation rule that fired from them — both share the same <paramref name="queued"/> batch so
    /// everything is saved and pushed together at the end of <see cref="ProcessAsync"/>.</summary>
    private async Task RecordAsync(
        IAppDbContext db, Issue issue, string projectKey, string actorUserId,
        IReadOnlyCollection<IssueChange> changes, Dictionary<string, Notification> queued, CancellationToken ct)
    {
        var reference = $"{projectKey}-{issue.Number}";
        var issueUrl = urls.Issue(tenant.Slug, issue.Id);
        var actorName = (await users.GetAsync(actorUserId, ct))?.DisplayName ?? "Someone";

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
