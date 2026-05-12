namespace Velune.Infrastructure.Pdf;

/// <summary>
/// Serializes access to PDFium operations via a single-permit semaphore.
/// </summary>
public sealed class PdfiumExecutionGate : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Acquires exclusive access to PDFium; dispose the returned handle to release.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the wait.</param>
    /// <returns>A disposable that releases the gate when disposed.</returns>
    public IDisposable Enter(CancellationToken cancellationToken = default)
    {
        _semaphore.Wait(cancellationToken);
        return new Releaser(_semaphore);
    }

    /// <inheritdoc />
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
