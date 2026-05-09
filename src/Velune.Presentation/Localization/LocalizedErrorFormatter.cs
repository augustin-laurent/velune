using Velune.Application.Results;

namespace Velune.Presentation.Localization;

/// <summary>
/// Formats application errors into localized title/message pairs using the active catalog.
/// </summary>
public sealed class LocalizedErrorFormatter : ILocalizedErrorFormatter
{
    private readonly ILocalizationService _localizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalizedErrorFormatter"/> class.
    /// </summary>
    /// <param name="localizationService">The localization service for string lookups.</param>
    public LocalizedErrorFormatter(ILocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(localizationService);

        _localizationService = localizationService;
    }

    /// <inheritdoc />
    public LocalizedErrorPresentation Format(
        AppError? appError,
        string fallbackTitleKey,
        string fallbackMessageKey,
        params object?[] fallbackArguments)
    {
        var fallbackTitle = _localizationService.GetString(fallbackTitleKey);
        var fallbackMessage = _localizationService.GetString(fallbackMessageKey, fallbackArguments);
        if (appError is null)
        {
            return new LocalizedErrorPresentation(fallbackTitle, fallbackMessage);
        }

        var titleKey = $"error.{appError.Code}.title";
        var messageKey = $"error.{appError.Code}.message";
        var hasLocalizedTitle = _localizationService.HasKey(titleKey);
        var hasLocalizedMessage = _localizationService.HasKey(messageKey);

        var title = hasLocalizedTitle
            ? _localizationService.GetString(titleKey)
            : fallbackTitle;
        var message = hasLocalizedMessage
            ? _localizationService.GetString(messageKey)
            : fallbackMessage;

        if (!hasLocalizedMessage &&
            !string.IsNullOrWhiteSpace(appError.Message) &&
            !string.Equals(appError.Message, fallbackMessage, StringComparison.Ordinal))
        {
            message = _localizationService.GetString("error.unhandled.wrap", fallbackMessage, appError.Message);
        }

        return new LocalizedErrorPresentation(title, message);
    }
}
