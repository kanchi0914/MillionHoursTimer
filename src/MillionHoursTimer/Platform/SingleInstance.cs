namespace MillionHoursTimer.Platform;

/// <summary>
/// 二重起動の防止。2つ目を起動したら、1つ目のウィンドウを前面に出す。
/// (旧バージョンとは別の名前なので、旧バージョンとの同時起動は <see cref="LegacyLocator"/> で別に確認する)
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\MillionHoursTimer";
    private const string ShowEventName = @"Local\MillionHoursTimer.Show";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showEvent;
    private RegisteredWaitHandle? _registration;

    private SingleInstance(Mutex mutex, EventWaitHandle showEvent)
    {
        _mutex = mutex;
        _showEvent = showEvent;
    }

    public static SingleInstance? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            if (EventWaitHandle.TryOpenExisting(ShowEventName, out var existing))
            {
                using (existing) existing.Set();
            }
            return null;
        }
        return new SingleInstance(mutex, new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName));
    }

    /// <summary>2つ目が起動されたときに呼ばれる処理 (別スレッドから呼ばれる)</summary>
    public void OnSecondInstance(Action action) =>
        _registration = ThreadPool.RegisterWaitForSingleObject(_showEvent, (_, _) => action(), null, Timeout.Infinite, executeOnlyOnce: false);

    public void Dispose()
    {
        _registration?.Unregister(null);
        _showEvent.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
