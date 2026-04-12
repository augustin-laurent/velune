using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Rendering;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Tests.Unit.Application.Rendering;

public sealed class RenderOrchestratorTests
{
    [Fact]
    public async Task Submit_ShouldReturnWithoutBlockingAndCompleteWithDuration()
    {
        var store = CreateStoreWithSession();
        var renderService = new ControlledRenderService();
        var releaseGate = renderService.EnqueueGate();
        using var orchestrator = new RenderOrchestrator(store, renderService);

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
        using var orchestrator = new RenderOrchestrator(store, renderService);

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
        using var orchestrator = new RenderOrchestrator(store, renderService);

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

    private static InMemoryDocumentSessionStore CreateStoreWithSession()
    {
        var store = new InMemoryDocumentSessionStore();
        store.SetCurrent(new DocumentSession(
            DocumentId.New(),
            new DocumentMetadata("test.pdf", "/tmp/test.pdf", DocumentType.Pdf, 1024, 4),
            ViewportState.Default));

        return store;
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
}
