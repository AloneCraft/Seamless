using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Tsavorite.core;

namespace Seamless.ScanIteratorFunctions;

public sealed class UnmanagedValueIterator<TValue> : ScanIteratorFunctions<TValue>
    where TValue : unmanaged {
#if TSAVORITE2_0BETA3
    private protected override TValue Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) {
        if(logRecord.ValueSpan.Length != Unsafe.SizeOf<TValue>()) {
            throw new InvalidOperationException($"Invalid value length for {typeof(TValue).Name}. Expected {Unsafe.SizeOf<TValue>()}, actual {logRecord.ValueSpan.Length}.");
        }
        return MemoryMarshal.Read<TValue>(logRecord.ValueSpan);
    }
#elif TSAVORITE1_1_1
    private protected override TValue Deserialize(ref SpanByte key, ref SpanByte value) {
        var bytes = value.AsReadOnlySpan();
        if(bytes.Length != Unsafe.SizeOf<TValue>()) {
            throw new InvalidOperationException($"Invalid value length for {typeof(TValue).Name}. Expected {Unsafe.SizeOf<TValue>()}, actual {bytes.Length}.");
        }
        return MemoryMarshal.Read<TValue>(bytes);
    }
#endif
}
