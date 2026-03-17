namespace Velune.Application.Results;

public class Result
{
    internal Result(bool isSuccess, AppError? error)
    {
        if (isSuccess && error is not null)
        {
            throw new ArgumentException("A successful result cannot contain an error.", nameof(error));
        }

        if (!isSuccess && error is null)
        {
            throw new ArgumentNullException(nameof(error), "A failure result must contain an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess
    {
        get;
    }
    public bool IsFailure => !IsSuccess;
    public AppError? Error
    {
        get;
    }
}

public sealed class Result<T> : Result
{
    internal Result(T value)
        : base(true, null)
    {
        Value = value;
    }

    internal Result(AppError error)
        : base(false, error)
    {
        Value = default;
    }

    public T? Value
    {
        get;
    }
}

public static class ResultFactory
{
    public static Result Success() => new(true, null);

    public static Result Failure(AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(false, error);
    }

    public static Result<T> Success<T>(T value) => new(value);

    public static Result<T> Failure<T>(AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(error);
    }
}
