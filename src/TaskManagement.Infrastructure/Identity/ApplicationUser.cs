using Microsoft.AspNetCore.Identity;

namespace TaskManagement.Infrastructure.Identity;

/// <summary>Identity user with the profile fields the task app needs for avatars and @mentions.</summary>
public class ApplicationUser : IdentityUser
{
    [PersonalData]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Deterministic hex colour for the user's initials avatar, assigned at registration.</summary>
    public string AvatarColor { get; set; } = "#64748b";

    public static string PickAvatarColor(string seed)
    {
        string[] palette = ["#ef4444", "#f97316", "#eab308", "#22c55e", "#14b8a6", "#3b82f6", "#6366f1", "#a855f7", "#ec4899"];
        var hash = 0;
        foreach (var c in seed)
            hash = unchecked(hash * 31 + c);
        return palette[Math.Abs(hash) % palette.Length];
    }
}
