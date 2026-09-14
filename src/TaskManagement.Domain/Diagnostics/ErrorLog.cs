using TaskManagement.Domain.Common;

namespace TaskManagement.Domain.Diagnostics;

/// <summary>
/// One captured Warning/Error/Critical log entry from the app's own code (not framework/EF/Npgsql
/// noise), written by <c>DatabaseLoggerProvider</c> so operators can see what went wrong from the
/// admin dashboard instead of grepping server logs. Deliberately not <see cref="ITenantScoped"/> —
/// an error isn't always attributable to one organization, and an admin needs to see all of them.
/// </summary>
public class ErrorLog : Entity
{
    private ErrorLog() { }

    public ErrorLog(string level, string category, string message, string? exception)
    {
        Level = Guard.NotBlank(level, nameof(level));
        Category = Guard.NotBlank(category, nameof(category));
        Message = message ?? string.Empty;
        Exception = exception;
    }

    public string Level { get; private set; } = string.Empty;

    /// <summary>The originating logger category, e.g. <c>TaskManagement.Infrastructure.Email.EmailDispatcher</c>.</summary>
    public string Category { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    /// <summary>Full <c>Exception.ToString()</c> (type, message, stack trace), when the log call included one.</summary>
    public string? Exception { get; private set; }
}
