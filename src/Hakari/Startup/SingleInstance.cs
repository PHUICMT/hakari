namespace Hakari.Startup;

/// <summary>Only one Hakari per signed-in user; a second start just exits.</summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\Hakari.Resident";

    private readonly Mutex mutex;

    private SingleInstance(Mutex mutex) => this.mutex = mutex;

    public static SingleInstance? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (createdNew)
        {
            return new SingleInstance(mutex);
        }

        mutex.Dispose();
        return null;
    }

    public void Dispose()
    {
        mutex.ReleaseMutex();
        mutex.Dispose();
    }
}
