using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Application.UseCases;
using AppResult = Velune.Application.Results.Result;

namespace Velune.Tests.Unit.Application.UseCases;

public sealed class PrintDocumentUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldFail_WhenFilePathIsEmpty()
    {
        var useCase = new PrintDocumentUseCase(new StubPrintService(ResultFactory.Success()));

        var result = await useCase.ExecuteAsync(new PrintDocumentRequest(string.Empty));

        Assert.True(result.IsFailure);
        Assert.Equal("print.path.empty", result.Error?.Code);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFail_WhenFileDoesNotExist()
    {
        var useCase = new PrintDocumentUseCase(new StubPrintService(ResultFactory.Success()));

        var result = await useCase.ExecuteAsync(new PrintDocumentRequest("/tmp/missing-document.pdf"));

        Assert.True(result.IsFailure);
        Assert.Equal("print.file.missing", result.Error?.Code);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldDelegateToPrintService_WhenFileExists()
    {
        var filePath = Path.GetTempFileName();
        try
        {
            var printService = new StubPrintService(ResultFactory.Success());
            var useCase = new PrintDocumentUseCase(printService);

            var result = await useCase.ExecuteAsync(new PrintDocumentRequest(filePath));

            Assert.True(result.IsSuccess);
            Assert.Equal(filePath, printService.LastRequest?.FilePath);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private sealed class StubPrintService : IPrintService
    {
        private readonly AppResult _result;

        public StubPrintService(AppResult result)
        {
            _result = result;
        }

        public PrintDocumentRequest? LastRequest { get; private set; }

        public bool SupportsSystemPrintDialog => false;

        public Task<AppResult> ShowSystemPrintDialogAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ResultFactory.Success());
        }

        public Task<Result<IReadOnlyList<PrintDestinationInfo>>> GetAvailablePrintersAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ResultFactory.Success<IReadOnlyList<PrintDestinationInfo>>([]));
        }

        public Task<AppResult> PrintAsync(PrintDocumentRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(_result);
        }
    }
}
