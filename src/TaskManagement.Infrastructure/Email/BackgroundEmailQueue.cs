using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaskManagement.Application.Abstractions;

namespace TaskManagement.Infrastructure.Email;

public sealed record OutgoingEmail(string ToEmail, string Subject, string HtmlBody);

/// <summary>
/// Hands an email to a background worker instead of talking to the SMTP server inline. Connecting,
/// authenticating and sending takes seconds against a real mail host, and doing it inside the request
/// made "Create issue" and "Send invitation" appear to hang — nothing the user is waiting on needs the
/// mail to have been delivered.
/// </summary>
public sealed class QueuedEmailSender(Channel<OutgoingEmail> queue, ILogger<QueuedEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
            return Task.CompletedTask;

        if (!queue.Writer.TryWrite(new OutgoingEmail(toEmail, subject, htmlBody)))
            logger.LogWarning("Email queue is full; dropped message to {To} ({Subject}).", toEmail, subject);

        return Task.CompletedTask;
    }
}

/// <summary>Drains the queue and performs the actual SMTP send, one message at a time.</summary>
public sealed class EmailDispatcher(
    Channel<OutgoingEmail> queue,
    SmtpEmailSender transport,
    ILogger<EmailDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                // SmtpEmailSender already logs and swallows delivery failures.
                await transport.SendAsync(message.ToEmail, message.Subject, message.HtmlBody, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected failure dispatching email to {To}.", message.ToEmail);
            }
        }
    }
}
