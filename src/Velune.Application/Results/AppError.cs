namespace Velune.Application.Results;

/// <summary>Represents a structured application error with a code, message, and type.</summary>
public sealed record AppError
{
    /// <summary>Gets the machine-readable error code.</summary>
    public required string Code
    {
        get; init;
    }

    /// <summary>Gets the human-readable error message.</summary>
    public required string Message
    {
        get; init;
    }

    /// <summary>Gets the category of the error.</summary>
    public ErrorType Type { get; init; } = ErrorType.Unexpected;

    /// <summary>Creates a validation error.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="message">The error message.</param>
    /// <returns>A new <see cref="AppError"/> of type <see cref="ErrorType.Validation"/>.</returns>
    public static AppError Validation(string code, string message) => new()
    {
        Code = code,
        Message = message,
        Type = ErrorType.Validation
    };

    /// <summary>Creates a not-found error.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="message">The error message.</param>
    /// <returns>A new <see cref="AppError"/> of type <see cref="ErrorType.NotFound"/>.</returns>
    public static AppError NotFound(string code, string message) => new()
    {
        Code = code,
        Message = message,
        Type = ErrorType.NotFound
    };

    /// <summary>Creates an unsupported-operation error.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="message">The error message.</param>
    /// <returns>A new <see cref="AppError"/> of type <see cref="ErrorType.Unsupported"/>.</returns>
    public static AppError Unsupported(string code, string message) => new()
    {
        Code = code,
        Message = message,
        Type = ErrorType.Unsupported
    };

    /// <summary>Creates an infrastructure error.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="message">The error message.</param>
    /// <returns>A new <see cref="AppError"/> of type <see cref="ErrorType.Infrastructure"/>.</returns>
    public static AppError Infrastructure(string code, string message) => new()
    {
        Code = code,
        Message = message,
        Type = ErrorType.Infrastructure
    };

    /// <summary>Creates an unexpected error.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="message">The error message.</param>
    /// <returns>A new <see cref="AppError"/> of type <see cref="ErrorType.Unexpected"/>.</returns>
    public static AppError Unexpected(string code, string message) => new()
    {
        Code = code,
        Message = message,
        Type = ErrorType.Unexpected
    };
}
