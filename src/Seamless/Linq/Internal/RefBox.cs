namespace Seamless.Linq.Carrier.Internal;

// store struct to heap
internal sealed class RefBox<T> : IDisposable
    where T : struct, IDisposable
{
    T value;
    bool isDisposed;

    public RefBox(T value)
    {
        this.value = value;
    }

    public ref T GetValueRef() => ref this.value;

    public void Dispose()
    {
        if (!this.isDisposed)
        {
            this.isDisposed = true;
            this.value.Dispose();
        }
    }
}
