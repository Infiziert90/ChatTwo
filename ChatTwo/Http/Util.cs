namespace ChatTwo.Http;

public static class WebserverUtil
{
    public static async Task<T> FrameworkWrapper<T>(Func<Task<T>> func)
    {
        return await Plugin.Framework.RunOnTick(func).ConfigureAwait(false);
    }

    public class DisposableWrapper : IDisposable {
        private readonly Action Down;
        private bool Disposed;

        public DisposableWrapper(Action down) {
            Down = down;
        }

        public void Dispose() {
            if (Disposed) return;

            Down();
            Disposed = true;

            GC.SuppressFinalize(this);
        }
    }
}

public static class AsyncUtils {
    public static async Task<IDisposable> UseWaitAsync(this SemaphoreSlim semaphore, CancellationToken ct = default) {
        await semaphore.WaitAsync(ct).ConfigureAwait(false);

        return new WebserverUtil.DisposableWrapper(() => {
            semaphore.Release();
        });
    }
}