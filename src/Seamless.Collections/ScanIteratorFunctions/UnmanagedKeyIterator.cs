using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Tsavorite.core;
namespace Seamless.ScanIteratorFunctions;
#if TSAVORITE2_0BETA3
public sealed class UnmanagedKeyIterator<TKey>:ScanIteratorFunctions<TKey>where TKey:unmanaged{
    private protected override TKey Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) {
        if(logRecord.Key.Length != Unsafe.SizeOf<TKey>()) {
            throw new InvalidOperationException($"Invalid key length for {typeof(TKey).Name}. Expected {Unsafe.SizeOf<TKey>()}, actual {logRecord.Key.Length}.");
        }
        return MemoryMarshal.Read<TKey>(logRecord.Key);
    }
}
#elif TSAVORITE1_1_1
public sealed class UnmanagedKeyIterator<TKey, TValue> : UnmanagedScanIteratorFunctions<TKey, TValue, TKey>
    where TKey : unmanaged
    where TValue : unmanaged
{
    private protected override TKey Deserialize(ref TKey key, ref TValue value) => key;
}
#endif

