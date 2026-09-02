namespace TaskManagement.Application.Common;

/// <summary>The requested entity does not exist, or is outside the caller's tenant.</summary>
public class NotFoundException(string message) : Exception(message)
{
    public static NotFoundException For<T>(object key) => new($"{typeof(T).Name} '{key}' was not found.");
}

/// <summary>The caller is authenticated but lacks the permission for this operation.</summary>
public class ForbiddenException(string message) : Exception(message)
{
    public static ForbiddenException Missing(object permission) => new($"You do not have permission to {permission}.");
}

/// <summary>A precondition or conflict that is expected and should surface to the user (e.g. duplicate project key).</summary>
public class ConflictException(string message) : Exception(message);
