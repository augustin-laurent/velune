using Velune.Application.Configuration;

namespace Velune.Application.Abstractions;

/// <summary>Manages loading and persisting user preferences.</summary>
public interface IUserPreferencesService
{
    /// <summary>Gets the current user preferences.</summary>
    UserPreferences Current
    {
        get;
    }

    /// <summary>Raised when preferences are saved.</summary>
    event EventHandler? PreferencesChanged;

    /// <summary>Persists the specified preferences and raises the changed event.</summary>
    /// <param name="preferences">The preferences to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(UserPreferences preferences, CancellationToken cancellationToken = default);
}
