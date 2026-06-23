namespace Seamless.System.Carrier.Internal;

// store struct to heap
internal sealed class RefBox<T>(T value): IDisposable
    where T : struct, IDisposable
{
    T value = value;
    bool isDisposed;

    public ref T GetValueRef() => ref value;

    public void Dispose()
    {
        if (!isDisposed)
        {
            isDisposed = true;
            value.Dispose();
        }
    }
}
