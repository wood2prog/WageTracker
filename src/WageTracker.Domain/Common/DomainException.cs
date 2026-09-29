namespace WageTracker.Domain.Common;

/// <summary>Thrown when an operation would violate a business rule.</summary>
public sealed class DomainException(string message) : Exception(message);
