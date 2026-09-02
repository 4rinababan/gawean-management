using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TaskManagement.Application.Abstractions;

namespace TaskManagement.Web.Infrastructure;

/// <summary>Per-user real-time channel. Clients join the group for their own id and receive a bump when a notification lands.</summary>
[Authorize]
public sealed class NotificationHub : Hub
{
    public const string HubUrl = "/hubs/notifications";

    public static string GroupFor(string userId) => $"user:{userId}";

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (userId is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(userId));

        await base.OnConnectedAsync();
    }
}

/// <summary>Server-side push: signal the recipient's circuits to refetch their notification list.</summary>
public sealed class SignalRNotificationRealtime(IHubContext<NotificationHub> hub) : INotificationRealtime
{
    public Task NotifyAsync(string recipientUserId, CancellationToken ct = default)
        => hub.Clients.Group(NotificationHub.GroupFor(recipientUserId)).SendAsync("notify", ct);
}
