using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Velune.Application.Abstractions;
using Velune.Application.Configuration;
using Velune.Application.DTOs;
using Velune.Application.Instrumentation;
using Velune.Application.Rendering;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;
using Velune.Tests.Unit.Support;

namespace Velune.Tests.Unit.Application.Rendering;

public sealed class RenderOrchestratorTests
{
    [Fact]
    public async Task Submit_ShouldReturnWithoutBlockingAndCompleteWithDuration()
    {
        var store = CreateStoreWithSession();
        var renderService = new ControlledRenderService();
        var releaseGate = renderService.EnqueueGate();
        using var orchestrator = new RenderOrchestrator(NoOpPerformanceMetrics.Instance, CreateCache(), new NullThumbnailDiskCache(), store, renderService);

        var handle = orchestrator.Submit(
            new RenderRequest("viewer", new PageIndex(0), 1.0, Rotation.Deg0));

        Assert.False(handle.Completion.IsCompleted);

        await Task.Delay(25);
        releaseGate.SetResult(true);

        var result = await handle.Completion;

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Page);
        Assert.True(result.Duration > TimeSpan.Zero);
    }

    [Fact]
    public async Task Cancel_ShouldCancelRunningJob()
    {
        var store = CreateStoreWithSession();
        var renderService = new ControlledRenderService();
        var releaseGate = renderService.EnqueueGate();
        using var orchestrator = new RenderOrchestrator(NoOpPerformanceMetrics.Instance, CreateCache(), new NullThumbnailDiskCache(), store, renderService);

        var handle = orchestrator.Submit(
            new RenderRequest("viewer", new PageIndex(0), 1.0, Rotation.Deg0));

        Assert.True(SpinWait.SpinUntil(() => renderService.InvocationCount == 1, TimeSpan.FromSeconds(1)));
        Assert.True(orchestrator.Cancel(handle.JobId));

        var result = await handle.Completion;

        Assert.True(result.IsCanceled);
        Assert.False(result.IsObsolete);

        releaseGate.TrySetResult(true);
    }

    [Fact]
    public async Task Submit_ShouldPurgeQueuedObsoleteJobs()
    {
        var store = CreateStoreWithSession();
        var renderService = new ControlledRenderService();
        var blockerGate = renderService.EnqueueGate();
        using var orchestrator = new RenderOrchestrator(NoOpPerformanceMetrics.Instance, CreateCache(), new NullThumbnailDiskCache(), store, renderService);

        var blocker = orchestrator.Submit(
            new RenderRequest("blocker", new PageIndex(0), 1.0, Rotation.Deg0));

        Assert.True(SpinWait.SpinUntil(() => renderService.InvocationCount == 1, TimeSpan.FromSeconds(1)));

        var obsolete = orchestrator.Submit(
            new RenderRequest("viewer", new PageIndex(0), 1.0, Rotation.Deg0));

        var current = orchestrator.Submit(
            new RenderRequest("viewer", new PageIndex(1), 1.0, Rotation.Deg0));

        var obsoleteResult = await obsolete.Completion;

        Assert.True(obsoleteResult.IsCanceled);
        Assert.True(obsoleteResult.IsObsolete);

        blockerGate.SetResult(true);

        var blockerResult = await blocker.Completion;
        var currentResult = await current.Completion;

        Assert.True(blockerResult.IsSuccess);
        Assert.True(currentResult.IsSuccess);
        Assert.Equal(2, renderService.InvocationCount);
    }

    [Fact]
    public async Task Submit_ShouldReuseThumbnailFromDiskCache()
    {
        var store = CreateStoreWithSession();
        var renderService = new ControlledRenderService();
        var diskCache = new StubThumbnailDiskCache(CreatePage(pageIndex: 0));
        using var orchestrator = new RenderOrchestrator(NoOpPerformanceMetrics.Instance, CreateCache(), diskCache, store, renderService);

        var handle = orchestrator.Submit(
            new RenderRequest("thumbnail:0", new PageIndex(0), 0.20, Rotation.Deg0, 170, 150));

        var result = await handle.Completion.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Page);
        Assert.Equal(0, renderService.InvocationCount);
        Assert.Equal(1, diskCache.HitCount);
    }

    [Fact]
    public async Task Submit_ShouldLogFirstPageRenderMetric_WhenViewerJobCompletes()
    {
        var logger = new ListLogger<DevelopmentPerformanceMetrics>();
        var metrics = new DevelopmentPerformanceMetrics(
            logger,
            Options.Create(new AppOptions()));
        var store = CreateStoreWithSession();
        var renderService = new ControlledRenderService();
        metrics.RecordDocumentOpened(store.Current!, TimeSpan.FromMilliseconds(12));
        using var orchestrator = new RenderOrchestrator(metrics, CreateCache(), new NullThumbnailDiskCache(), store, renderService);

        var handle = orchestrator.Submit(
            new RenderRequest("viewer", new PageIndex(0), 1.0, Rotation.Deg0));
        var result = await handle.Completion;

        Assert.True(result.IsSuccess);
        Assert.Contains(
            logger.Entries,
            entry => entry.LogLevel == Microsoft.Extensions.Logging.LogLevel.Information &&
                     entry.Message.Contains("FirstPageRender", StringComparison.Ordinal) &&
                     entry.Message.Contains("TimeToFirstPageMs", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Submit_ShouldLogThumbnailMetric_WhenThumbnailJobCompletes()
    {
        var logger = new ListLogger<DevelopmentPerformanceMetrics>();
        var metrics = new DevelopmentPerformanceMetrics(
            logger,
            Options.Create(new AppOptions()));
        var store = CreateStoreWithSession();
        var renderService = new ControlledRenderService();
        using var orchestrator = new RenderOrchestrator(metrics, CreateCache(), new NullThumbnailDiskCache(), store, renderService);

        var handle = orchestrator.Submit(
            new RenderRequest("thumbnail:0", new PageIndex(0), 0.20, Rotation.Deg0, 170, 150));
        var result = await handle.Completion;

        Assert.True(result.IsSuccess);
        Assert.Contains(
            logger.Entries,
            entry => entry.LogLevel == Microsoft.Extensions.Logging.LogLevel.Information &&
                     entry.Message.Contains("ThumbnailRender", StringComparison.Ordinal) &&
                     entry.Message.Contains("ManagedMemoryMb", StringComparison.Ordinal));
    }

    private static InMemoryDocumentSessionStore CreateStoreWithSession()
    {
        var store = new InMemoryDocumentSessionStore();
        store.SetCurrent(new DocumentSession(
            DocumentId.New(),
            new DocumentMetadata("test.pdf", "/tmp/test.pdf", DocumentType.Pdf, 1024, 4),
            ViewportState.Default));

        return store;
    }

    private static IRenderMemoryCache CreateCache()
    {
        return new RenderMemoryCache(
            NullLogger<RenderMemoryCache>.Instance,
            new StubUserPreferencesService(8));
    }

    private static RenderedPage CreatePage(int pageIndex)
    {
        return new RenderedPage(
            new PageIndex(pageIndex),
            [0, 0, 0, 255],
            1,
            1);
    }

    private sealed class ControlledRenderService : IRenderService
    {
        private readonly object _gate = new();
        private readonly Queue<TaskCompletionSource<bool>> _releaseGates = [];
        private int _invocationCount;

        public int InvocationCount => Volatile.Read(ref _invocationCount);

        public TaskCompletionSource<bool> EnqueueGate()
        {
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_gate)
            {
                _releaseGates.Enqueue(gate);
            }

            return gate;
        }

        public async Task<RenderedPage> RenderPageAsync(
            IDocumentSession session,
            PageIndex pageIndex,
            double zoomFactor,
            Rotation rotation,
            CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<bool>? releaseGate = null;

            lock (_gate)
            {
                Interlocked.Increment(ref _invocationCount);

                if (_releaseGates.Count > 0)
                {
                    releaseGate = _releaseGates.Dequeue();
                }
            }

            if (releaseGate is not null)
            {
                await releaseGate.Task.WaitAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            return new RenderedPage(
                pageIndex,
                [0, 0, 0, 255],
                1,
                1);
        }
    }

    private sealed class StubUserPreferencesService : IUserPreferencesService
    {
        public StubUserPreferencesService(int memoryCacheEntryLimit)
        {
            Current = UserPreferences.CreateDefault(memoryCacheEntryLimit);
        }

        public UserPreferences Current { get; private set; }

        public event EventHandler? PreferencesChanged;

        public Task SaveAsync(UserPreferences preferences, CancellationToken cancellationToken = default)
        {
            Current = preferences;
            PreferencesChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }

    private sealed class StubThumbnailDiskCache : IThumbnailDiskCache
    {
        private readonly RenderedPage _renderedPage;

        public StubThumbnailDiskCache(RenderedPage renderedPage)
        {
            ArgumentNullException.ThrowIfNull(renderedPage);
            _renderedPage = renderedPage;
        }

        public int HitCount
        {
            get;
            private set;
        }

        public bool TryGet(
            IDocumentSession session,
            RenderRequest request,
            out RenderedPage? renderedPage)
        {
            ArgumentNullException.ThrowIfNull(session);
            ArgumentNullException.ThrowIfNull(request);

            HitCount++;
            renderedPage = _renderedPage;
            return true;
        }

        public void Store(
            IDocumentSession session,
            RenderRequest request,
            RenderedPage renderedPage)
        {
        }
    }
}
