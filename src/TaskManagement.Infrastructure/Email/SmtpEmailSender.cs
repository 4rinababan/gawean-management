using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using TaskManagement.Application.Abstractions;

namespace TaskManagement.Infrastructure.Email;

/// <summary>SMTP-backed <see cref="IEmailSender"/> (MailKit). Falls back to logging the message when SMTP is not configured.</summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
            return;

        if (!_options.IsConfigured)
        {
            logger.LogInformation("SMTP not configured. Would send to {To}: {Subject}\n{Body}", toEmail, subject, htmlBody);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(_options.Host, _options.Port, ResolveSocketOptions(), ct);

            if (!string.IsNullOrEmpty(_options.User))
                await client.AuthenticateAsync(_options.User, _options.Password, ct);

            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (Exception ex)
        {
            // Never let a mail-server problem break the operation that triggered the email
            // (invites, assignments and comments still succeed; the recipient just misses the notice).
            logger.LogError(ex, "Failed to send email to {To} ({Subject}) via {Host}:{Port}.", toEmail, subject, _options.Host, _options.Port);
        }
    }

    private SecureSocketOptions ResolveSocketOptions()
    {
        if (Enum.TryParse<SecureSocketOptions>(_options.Security, ignoreCase: true, out var explicitMode)
            && explicitMode != SecureSocketOptions.Auto)
        {
            return explicitMode;
        }

        // Auto: port 465 is implicit TLS; everything else negotiates STARTTLS.
        return _options.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
    }
}
