using Microsoft.Extensions.Options;
using Velune.Application.Abstractions;
using Velune.Application.Configuration;
using Velune.Application.Instrumentation;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Application.UseCases;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;
using Velune.Tests.Unit.Support;

namespace Velune.Tests.Unit.Application.UseCases;

public sealed class OpenDocumentUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationError_WhenPathIsEmpty()
    {
        var opener = new ThrowingDocumentOpener();
        var store = new InMemoryDocumentSessionStore();
        var useCase = new OpenDocumentUseCase(opener, store, NoOpPerformanceMetrics.Instance);

        var result = await useCase.ExecuteAsync(new OpenDocumentRequest(string.Empty));

        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal("document.path.empty", result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFoundError_WhenFileIsMissing()
    {
        var opener = new ThrowingDocumentOpener(new FileNotFoundException("Missing file"));
        var store = new InMemoryDocumentSessionStore();
        var useCase = new OpenDocumentUseCase(opener, store, NoOpPerformanceMetrics.Instance);

        var result = await useCase.ExecuteAsync(new OpenDocumentRequest("/tmp/missing.pdf"));

        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal("document.file.missing", result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnUnsupportedError_WhenFormatIsUnsupported()
    {
        var opener = new ThrowingDocumentOpener(new NotSupportedException("Unsupported format"));
        var store = new InMemoryDocumentSessionStore();
        var useCase = new OpenDocumentUseCase(opener, store, NoOpPerformanceMetrics.Instance);

        var result = await useCase.ExecuteAsync(new OpenDocumentRequest("/tmp/file.xyz"));

        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal("document.format.unsupported", result.Error.Code);
        Assert.Equal(ErrorType.Unsupported, result.Error.Type);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnInfrastructureError_WhenOpenFails()
    {
        var opener = new ThrowingDocumentOpener(new InvalidOperationException("PDFium failed"));
        var store = new InMemoryDocumentSessionStore();
        var useCase = new OpenDocumentUseCase(opener, store, NoOpPerformanceMetrics.Instance);

        var result = await useCase.ExecuteAsync(new OpenDocumentRequest("/tmp/file.pdf"));

        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal("document.open.failed", result.Error.Code);
        Assert.Equal(ErrorType.Infrastructure, result.Error.Type);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldLogDocumentOpenMetric_WhenOpenSucceeds()
    {
        var logger = new ListLogger<DevelopmentPerformanceMetrics>();
        var metrics = new DevelopmentPerformanceMetrics(
            logger,
            Options.Create(new AppOptions()));
        var session = new DocumentSession(
            DocumentId.New(),
            new DocumentMetadata("test.pdf", "/tmp/test.pdf", DocumentType.Pdf, 1024, 4),
            ViewportState.Default);
        var opener = new ReturningDocumentOpener(session);
        var store = new InMemoryDocumentSessionStore();
        var useCase = new OpenDocumentUseCase(opener, store, metrics);

        var result = await useCase.ExecuteAsync(new OpenDocumentRequest("/tmp/test.pdf"));

        Assert.True(result.IsSuccess);
        Assert.Same(session, store.Current);
        Assert.Contains(
            logger.Entries,
            entry => entry.LogLevel == Microsoft.Extensions.Logging.LogLevel.Information &&
                     entry.Message.Contains("DocumentOpen", StringComparison.Ordinal) &&
                     entry.Message.Contains("ManagedMemoryMb", StringComparison.Ordinal));
    }

    private sealed class ThrowingDocumentOpener : IDocumentOpener
    {
        private readonly Exception? _exception;

        public ThrowingDocumentOpener(Exception? exception = null)
        {
            _exception = exception;
        }

        public Task<IDocumentSession> OpenAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            if (_exception is not null)
            {
                throw _exception;
            }

            throw new InvalidOperationException("No session configured.");
        }
    }

    private sealed class ReturningDocumentOpener : IDocumentOpener
    {
        private readonly IDocumentSession _session;

        public ReturningDocumentOpener(IDocumentSession session)
        {
            ArgumentNullException.ThrowIfNull(session);
            _session = session;
        }

        public Task<IDocumentSession> OpenAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_session);
        }
    }
}
