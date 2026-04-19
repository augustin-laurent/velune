using Velune.Application.Configuration;

namespace Velune.Application.Abstractions;

public interface IUserPreferencesService
{
    UserPreferences Current
    {
        get;
    }

    event EventHandler? PreferencesChanged;

    Task SaveAsync(UserPreferences preferences, CancellationToken cancellationToken = default);
}
