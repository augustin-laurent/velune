using Velune.Application.Results;

namespace Velune.Application.Rendering;

internal static class RenderErrorMapper
{
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
