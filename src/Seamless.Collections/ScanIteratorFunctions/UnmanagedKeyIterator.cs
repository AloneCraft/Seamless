using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Tsavorite.core;
namespace Seamless.ScanIteratorFunctions;
public sealed class UnmanagedKeyIterator<TKey>:ScanIteratorFunctions<TKey>where TKey:unmanaged{

#if TSAVORITE2_0BETA3
    private protected override TKey Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) {
        if(logRecord.Key.Length != Unsafe.SizeOf<TKey>()) {
            throw new InvalidOperationException($"Invalid key length for {typeof(TKey).Name}. Expected {Unsafe.SizeOf<TKey>()}, actual {logRecord.Key.Length}.");
        }
        return MemoryMarshal.Read<TKey>(logRecord.Key);
    }
#elif TSAVORITE1_1_1
    private protected override TKey Deserialize(ref SpanByte key, ref SpanByte value) {
        var bytes = key.AsReadOnlySpan();
        if(bytes.Length != Unsafe.SizeOf<TKey>()) {
            throw new InvalidOperationException($"Invalid key length for {typeof(TKey).Name}. Expected {Unsafe.SizeOf<TKey>()}, actual {bytes.Length}.");
        }
        return MemoryMarshal.Read<TKey>(bytes);
    }
#endif
}

