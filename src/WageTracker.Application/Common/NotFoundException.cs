namespace WageTracker.Application.Common;

/// <summary>
/// Thrown when a use case refers to something that does not exist. The UI shows its message, as it
/// does for <see cref="Domain.Common.DomainException"/>.
/// </summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>Thrown by use cases that need payroll settings before they have been set up.</summary>
public sealed class SettingsNotConfiguredException()
    : Exception("Payroll settings have not been set up yet. Open the settings page first.");
