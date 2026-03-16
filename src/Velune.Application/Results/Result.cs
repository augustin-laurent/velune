namespace Velune.Application.Results;

public sealed class Result<T>
{
    internal Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public bool IsSuccess
    {
        get;
    }
    public bool IsFailure => !IsSuccess;
    public T? Value
    {
        get;
    }
    public string? Error
    {
        get;
    }
}

public static class Result
{
    public static Result<T> Success<T>(T value) => new(true, value, null);

    public static Result<T> Failure<T>(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new Result<T>(false, default, error);
    }
}
