namespace TaskManagement.Domain.Common;

/// <summary>Thrown when an operation would violate a domain invariant (e.g. an illegal sprint transition).</summary>
public class DomainException(string message) : Exception(message);
