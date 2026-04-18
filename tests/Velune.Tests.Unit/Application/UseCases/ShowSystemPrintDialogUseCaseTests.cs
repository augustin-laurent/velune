using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Application.UseCases;
using AppResult = Velune.Application.Results.Result;

namespace Velune.Tests.Unit.Application.UseCases;

public sealed class ShowSystemPrintDialogUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldFail_WhenFilePathIsEmpty()
    {
        var useCase = new ShowSystemPrintDialogUseCase(new StubPrintService(ResultFactory.Success()));

        var result = await useCase.ExecuteAsync(string.Empty);

        Assert.True(result.IsFailure);
        Assert.Equal("print.path.empty", result.Error?.Code);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFail_WhenFileDoesNotExist()
    {
        var useCase = new ShowSystemPrintDialogUseCase(new StubPrintService(ResultFactory.Success()));

        var result = await useCase.ExecuteAsync("/tmp/missing-document.pdf");

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
            var useCase = new ShowSystemPrintDialogUseCase(printService);

            var result = await useCase.ExecuteAsync(filePath);

            Assert.True(result.IsSuccess);
            Assert.Equal(filePath, printService.LastSystemDialogFilePath);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private sealed class StubPrintService : IPrintService
    {
        private readonly AppResult _systemDialogResult;

        public StubPrintService(AppResult systemDialogResult)
        {
            _systemDialogResult = systemDialogResult;
        }

        public bool SupportsSystemPrintDialog => true;

        public string? LastSystemDialogFilePath { get; private set; }

        public Task<AppResult> ShowSystemPrintDialogAsync(string filePath, CancellationToken cancellationToken = default)
        {
            LastSystemDialogFilePath = filePath;
            return Task.FromResult(_systemDialogResult);
        }

        public Task<Result<IReadOnlyList<PrintDestinationInfo>>> GetAvailablePrintersAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ResultFactory.Success<IReadOnlyList<PrintDestinationInfo>>([]));
        }

        public Task<AppResult> PrintAsync(PrintDocumentRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ResultFactory.Success());
        }
    }
}
