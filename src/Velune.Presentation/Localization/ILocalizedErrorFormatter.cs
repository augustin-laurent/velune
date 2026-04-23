using Velune.Application.Results;

namespace Velune.Presentation.Localization;

public interface ILocalizedErrorFormatter
{
    LocalizedErrorPresentation Format(
        AppError? appError,
        string fallbackTitleKey,
        string fallbackMessageKey,
        params object?[] fallbackArguments);
}
