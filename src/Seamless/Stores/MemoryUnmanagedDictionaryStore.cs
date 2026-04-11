using Seamless.Collections;
using Seamless.Devices;

namespace Seamless.Stores;

public sealed class MemoryUnmanagedDictionaryStore<TKey,TValue>:UnmanagedDictionaryStore<TKey,TValue>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {
    internal MemoryUnmanagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction):
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,new MemoryOnlyDevice(SegmentSize*2),new MemoryOnlyDevice(Const.MinObjectLogCapacity),null) {
    }

    public override UnmanagedDictionary<TKey,TValue,UnmanagedSerializer<TKey>,UnmanagedSerializer<TValue>> Create() => new MemoryUnmanagedDictionary<TKey,TValue>(this);
}
