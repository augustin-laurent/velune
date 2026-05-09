using Velune.Application.Results;

namespace Velune.Application.Rendering;

/// <summary>Maps render-related exceptions to structured application errors.</summary>
internal static class RenderErrorMapper
{
    /// <summary>Maps the given exception to an appropriate <see cref="AppError"/>.</summary>
    /// <param name="exception">The exception to map.</param>
    /// <returns>An <see cref="AppError"/> representing the failure.</returns>
    public static AppError Map(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            ObjectDisposedException => AppError.Infrastructure(
                "document.render.disposed",
                "The document render resources are no longer available."),

            InvalidOperationException => AppError.Infrastructure(
                "document.render.failed",
                exception.Message),

            NotSupportedException => AppError.Unsupported(
                "document.render.unsupported",
                exception.Message),

            _ => AppError.Unexpected(
                "document.render.unexpected",
                exception.Message)
        };
    }
}
