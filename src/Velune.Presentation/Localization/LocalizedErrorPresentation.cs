namespace Velune.Presentation.Localization;

/// <summary>
/// Represents a user-facing error with localized title and message.
/// </summary>
/// <param name="Title">The localized error title.</param>
/// <param name="Message">The localized error message.</param>
public sealed record LocalizedErrorPresentation(
    string Title,
    string Message);
