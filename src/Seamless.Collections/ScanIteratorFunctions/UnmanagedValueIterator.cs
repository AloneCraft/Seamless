using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Tsavorite.core;

namespace Seamless.ScanIteratorFunctions;
#if TSAVORITE2_0BETA3
public sealed class UnmanagedValueIterator<TValue> : ScanIteratorFunctions<TValue>
    where TValue : unmanaged {
    private protected override TValue Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) {
        if(logRecord.ValueSpan.Length != Unsafe.SizeOf<TValue>()) {
            throw new InvalidOperationException($"Invalid value length for {typeof(TValue).Name}. Expected {Unsafe.SizeOf<TValue>()}, actual {logRecord.ValueSpan.Length}.");
        }
        return MemoryMarshal.Read<TValue>(logRecord.ValueSpan);
    }
}
#elif TSAVORITE1_1_1
public sealed class UnmanagedValueIterator<TKey, TValue> : UnmanagedScanIteratorFunctions<TKey, TValue, TValue>
    where TKey : unmanaged
    where TValue : unmanaged
{
    private protected override TValue Deserialize(ref TKey key, ref TValue value) => value;
}
#endif
