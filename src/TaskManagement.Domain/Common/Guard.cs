namespace TaskManagement.Domain.Common;

/// <summary>Small argument-validation helpers that raise <see cref="DomainException"/> so callers surface a consistent error.</summary>
public static class Guard
{
    public static string NotBlank(string? value, string paramName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new DomainException($"{paramName} must not be empty.")
            : value.Trim();

    public static T NotNull<T>(T? value, string paramName) where T : class
        => value ?? throw new DomainException($"{paramName} must not be null.");

    public static int InRange(int value, int min, int max, string paramName)
        => value < min || value > max
            ? throw new DomainException($"{paramName} must be between {min} and {max}.")
            : value;
}
