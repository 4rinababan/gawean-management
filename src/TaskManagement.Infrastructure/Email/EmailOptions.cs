namespace TaskManagement.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;

    /// <summary>
    /// TLS mode. Leave as "Auto" to pick from the port (465 → implicit SSL, otherwise STARTTLS).
    /// Accepts any <c>MailKit.Security.SecureSocketOptions</c> name (e.g. "SslOnConnect", "StartTls", "None").
    /// </summary>
    public string Security { get; set; } = "Auto";

    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = "no-reply@example.com";
    public string FromName { get; set; } = "Task Management";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
}
