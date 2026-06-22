namespace Seamless.Tsavorite.Collections.Devices;
/// <summary>
/// ディスクI/Oを行わず、退避ページをヒープ上のバイト配列に保持するデバイス。
/// </summary>
public sealed class MemoryOnlyDevice(long Capacity):StorageDeviceBase(DeviceName,DefaultSectorSize,Capacity) {
    private const string DeviceName = "memory-only";
    private const uint DefaultSectorSize = 512;

    public override void ReadAsync(int segmentId,ulong sourceAddress,IntPtr destinationAddress,uint readLength,DeviceIOCompletionCallback callback,object context) {
        throw new IOException(nameof(ReadAsync));
    }
    public override void Dispose() {
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
        throw new OutOfMemoryException(nameof(WriteAsync));
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
    }
}
