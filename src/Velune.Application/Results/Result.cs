namespace Velune.Application.Results;

/// <summary>Represents the outcome of an operation that can succeed or fail.</summary>
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

    /// <summary>Gets whether the operation succeeded.</summary>
    public bool IsSuccess
    {
        get;
    }

    /// <summary>Gets whether the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>Gets the error if the operation failed; otherwise <c>null</c>.</summary>
    public AppError? Error
    {
        get;
    }
}

/// <summary>Represents the outcome of an operation that produces a value on success.</summary>
/// <typeparam name="T">The type of the success value.</typeparam>
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

    /// <summary>Gets the success value, or <c>default</c> if the result is a failure.</summary>
    public T? Value
    {
        get;
    }
}

/// <summary>Factory methods for creating <see cref="Result"/> and <see cref="Result{T}"/> instances.</summary>
public static class ResultFactory
{
    /// <summary>Creates a successful result with no value.</summary>
    /// <returns>A successful <see cref="Result"/>.</returns>
    public static Result Success() => new(true, null);

    /// <summary>Creates a failed result with the specified error.</summary>
    /// <param name="error">The error describing the failure.</param>
    /// <returns>A failed <see cref="Result"/>.</returns>
    public static Result Failure(AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(false, error);
    }

    /// <summary>Creates a successful result containing the specified value.</summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="value">The success value.</param>
    /// <returns>A successful <see cref="Result{T}"/>.</returns>
    public static Result<T> Success<T>(T value) => new(value);

    /// <summary>Creates a failed result with the specified error.</summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="error">The error describing the failure.</param>
    /// <returns>A failed <see cref="Result{T}"/>.</returns>
    public static Result<T> Failure<T>(AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(error);
    }
}
