namespace LinkedInAutoReply.Services;

/// <summary>
/// Allows the UI to trigger an immediate mail scan instead of waiting for the next poll.
/// </summary>
public class ScanTriggerService
{
    private readonly SemaphoreSlim _signal = new(0, 1);
    public event Action? ScanCompleted;

    public void TriggerScan()
    {
        try { _signal.Release(); }
        catch (SemaphoreFullException) { /* already signalled, ignore */ }
    }

    public void NotifyScanCompleted() => ScanCompleted?.Invoke();

    public async Task WaitAsync(TimeSpan pollInterval, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(pollInterval);
        try
        {
            await _signal.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Normal timeout — continue
        }
    }
}
