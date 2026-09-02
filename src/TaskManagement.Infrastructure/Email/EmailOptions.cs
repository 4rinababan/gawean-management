namespace TaskManagement.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = "no-reply@example.com";
    public string FromName { get; set; } = "Task Management";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
}
