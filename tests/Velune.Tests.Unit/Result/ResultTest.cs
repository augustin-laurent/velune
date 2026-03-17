using Velune.Application.Results;

namespace Velune.Tests.Unit.Result;

public sealed class ResultTest
{
    [Fact]
    public void Success_ShouldCreateSuccessfulNonGenericResult()
    {
        var result = ResultFactory.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_ShouldCreateFailedNonGenericResult()
    {
        var error = AppError.Validation("validation.failed", "Validation failed.");

        var result = ResultFactory.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void GenericSuccess_ShouldCreateSuccessfulResultWithValue()
    {
        var result = ResultFactory.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void GenericFailure_ShouldCreateFailedResultWithError()
    {
        var error = AppError.NotFound("document.missing", "Document not found.");

        var result = ResultFactory.Failure<string>(error);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
        Assert.Null(result.Value);
    }
}
