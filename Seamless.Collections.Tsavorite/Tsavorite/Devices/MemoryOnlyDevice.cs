namespace Seamless.Tsavorite.Devices;
/// <summary>
/// ディスクI/Oを行わず、退避ページをヒープ上のバイト配列に保持するデバイス。
/// </summary>
public sealed class MemoryOnlyDevice(long Capacity,byte[]? BackingBuffer = null):StorageDeviceBase(DeviceName,DefaultSectorSize,Capacity) {
    private const string DeviceName = "memory-only";
    private const uint DefaultSectorSize = 512;
    private readonly byte[] buffer = ResolveBuffer(Capacity,BackingBuffer);
    private bool disposed;

    public override void ReadAsync(int segmentId,ulong sourceAddress,IntPtr destinationAddress,uint readLength,DeviceIOCompletionCallback callback,object context) {
        this.ThrowIfDisposed();
        this.ThrowIfOutOfRange(sourceAddress,readLength);
        Marshal.Copy(this.buffer,checked((int)sourceAddress),destinationAddress,checked((int)readLength));
        callback(0,readLength,context);
    }
    public override void Dispose() {
        this.disposed=true;
    }
    /// <summary>
    /// メモリ上のバイト配列に書き込む。既存のページがあれば上書きするが、容量を超える場合は例外を投げる。
    /// </summary>
    /// <param name="sourceAddress"></param>
    /// <param name="segmentId"></param>
    /// <param name="destinationAddress"></param>
    /// <param name="numBytesToWrite"></param>
    /// <param name="callback"></param>
    /// <param name="context"></param>
    /// <exception cref="OutOfMemoryException"></exception>
    public override void WriteAsync(IntPtr sourceAddress,int segmentId,ulong destinationAddress,uint numBytesToWrite,DeviceIOCompletionCallback callback,object context) {
        this.ThrowIfDisposed();
        this.ThrowIfOutOfRange(destinationAddress,numBytesToWrite);
        Marshal.Copy(sourceAddress,this.buffer,checked((int)destinationAddress),checked((int)numBytesToWrite));
        callback(0,numBytesToWrite,context);
    }
    /// <summary>
    /// HandCapacity を超える書き込み要求があった場合に呼び出される。セグメントに属するページをすべて解放し、確保量を減算する。
    /// Log.WriteAsync で確保したバイト配列を解放する。セグメントに属するページをすべて解放し、確保量を減算する。
    /// Log.TruncateAsync でセグメントを切り詰める場合も同様に呼び出される想定。
    /// </summary>
    /// <param name="segment"></param>
    /// <param name="callback"></param>
    /// <param name="result"></param>
    public override void RemoveSegmentAsync(int segment,AsyncCallback callback,IAsyncResult result) {
        Trace.WriteLine(nameof(RemoveSegmentAsync));
        callback(result);
    }

    private void ThrowIfDisposed() {
        if(this.disposed) {
            throw new ObjectDisposedException(nameof(MemoryOnlyDevice));
        }
    }

    private void ThrowIfOutOfRange(ulong address,uint length) {
        var end = checked(address+length);
        if(end>(ulong)this.buffer.Length) {
            throw new OutOfMemoryException(nameof(MemoryOnlyDevice));
        }
    }

    private static byte[] ResolveBuffer(long capacity,byte[]? backingBuffer) {
        var checkedCapacity = checked((int)capacity);
        if(backingBuffer is null) {
            return new byte[checkedCapacity];
        }
        if(backingBuffer.Length!=checkedCapacity) {
            throw new ArgumentException("BackingBuffer length must match Capacity.",nameof(BackingBuffer));
        }
        return backingBuffer;
    }
}
