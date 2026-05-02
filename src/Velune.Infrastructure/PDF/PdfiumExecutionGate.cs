namespace Velune.Infrastructure.Pdf;

public sealed class PdfiumExecutionGate : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public IDisposable Enter(CancellationToken cancellationToken = default)
    {
        _semaphore.Wait(cancellationToken);
        return new Releaser(_semaphore);
    }

    public void Dispose()
    {
        _semaphore.Dispose();
    }

    private sealed class Releaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private int _released;

        public Releaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                _semaphore.Release();
            }
        }
    }
}
