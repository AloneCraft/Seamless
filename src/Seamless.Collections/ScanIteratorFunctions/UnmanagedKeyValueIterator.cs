using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Tsavorite.core;
namespace Seamless.ScanIteratorFunctions;
public sealed class UnmanagedKeyValueIterator<TKey, TValue> :
#if TSAVORITE2_0BETA3
    ScanIteratorFunctions<KeyValuePair<TKey, TValue>>
#elif TSAVORITE1_1_1
    UnmanagedScanIteratorFunctions<TKey, TValue, KeyValuePair<TKey, TValue>>
#endif
    where TKey : unmanaged
    where TValue : unmanaged{
#if TSAVORITE2_0BETA3
    private protected override KeyValuePair<TKey, TValue> Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) {
        if(logRecord.Key.Length != Unsafe.SizeOf<TKey>()) {
            throw new InvalidOperationException($"Invalid key length for {typeof(TKey).Name}. Expected {Unsafe.SizeOf<TKey>()}, actual {logRecord.Key.Length}.");
        }
        if(logRecord.ValueSpan.Length != Unsafe.SizeOf<TValue>()) {
            throw new InvalidOperationException($"Invalid value length for {typeof(TValue).Name}. Expected {Unsafe.SizeOf<TValue>()}, actual {logRecord.ValueSpan.Length}.");
        }
        var key = MemoryMarshal.Read<TKey>(logRecord.Key);
        var value = MemoryMarshal.Read<TValue>(logRecord.ValueSpan);
        return new(key, value);
    }
#elif TSAVORITE1_1_1
    private protected override KeyValuePair<TKey, TValue> Deserialize(ref TKey key, ref TValue value) => new(key, value);
#endif
}
