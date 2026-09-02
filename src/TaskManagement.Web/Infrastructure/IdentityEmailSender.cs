using Microsoft.AspNetCore.Identity;
using AppEmail = TaskManagement.Application.Abstractions.IEmailSender;
using TaskManagement.Infrastructure.Identity;

namespace TaskManagement.Web.Infrastructure;

/// <summary>Routes Identity's confirmation / password-reset mails through the app's configured <see cref="AppEmail"/> (SMTP).</summary>
public sealed class IdentityEmailSender(AppEmail email) : IEmailSender<ApplicationUser>
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string emailAddress, string confirmationLink)
        => email.SendAsync(emailAddress, "Confirm your email",
            $"<p>Welcome! Please confirm your account by <a href=\"{confirmationLink}\">clicking here</a>.</p>");

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string emailAddress, string resetLink)
        => email.SendAsync(emailAddress, "Reset your password",
            $"<p>Reset your password by <a href=\"{resetLink}\">clicking here</a>.</p>");

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string emailAddress, string resetCode)
        => email.SendAsync(emailAddress, "Reset your password",
            $"<p>Your password reset code is: <strong>{resetCode}</strong></p>");
}
