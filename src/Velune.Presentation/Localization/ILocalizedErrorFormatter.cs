using Velune.Application.Results;

namespace Velune.Presentation.Localization;

/// <summary>
/// Formats application errors into user-facing localized title/message pairs.
/// </summary>
public interface ILocalizedErrorFormatter
{
    /// <summary>
    /// Formats the given error into a localized presentation with title and message.
    /// </summary>
    /// <param name="appError">The application error, or null for a generic message.</param>
    /// <param name="fallbackTitleKey">Localization key for the fallback title.</param>
    /// <param name="fallbackMessageKey">Localization key for the fallback message.</param>
    /// <param name="fallbackArguments">Format arguments for the fallback message.</param>
    /// <returns>A localized error presentation.</returns>
    LocalizedErrorPresentation Format(
        AppError? appError,
        string fallbackTitleKey,
        string fallbackMessageKey,
        params object?[] fallbackArguments);
}
