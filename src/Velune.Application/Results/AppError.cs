namespace Velune.Application.Results;

public sealed record AppError
{
    public required string Code
    {
        get; init;
    }
    public required string Message
    {
        get; init;
    }
    public ErrorType Type { get; init; } = ErrorType.Unexpected;

    public static AppError Validation(string code, string message) => new()
    {
        Code = code,
        Message = message,
        Type = ErrorType.Validation
    };

    public static AppError NotFound(string code, string message) => new()
    {
        Code = code,
        Message = message,
        Type = ErrorType.NotFound
    };

    public static AppError Unsupported(string code, string message) => new()
    {
        Code = code,
        Message = message,
        Type = ErrorType.Unsupported
    };

    public static AppError Infrastructure(string code, string message) => new()
    {
        Code = code,
        Message = message,
        Type = ErrorType.Infrastructure
    };

    public static AppError Unexpected(string code, string message) => new()
    {
        Code = code,
        Message = message,
        Type = ErrorType.Unexpected
    };
}
