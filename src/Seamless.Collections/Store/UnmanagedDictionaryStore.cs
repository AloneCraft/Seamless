using Seamless.Devices;
using Tsavorite.core;

namespace Seamless.Collections;

public abstract class UnmanagedDictionaryStore<TKey, TValue> : CollectionStore<TKey, TValue>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue> {

    private protected UnmanagedDictionaryStore(long IndexSize, long PageSize, long SegmentSize, long MemorySize, double MutableFraction, IDevice LogDevice, IDevice ObjectLogDevice, string? CheckpointDir)
        : base(IndexSize, PageSize, SegmentSize, MemorySize, MutableFraction, LogDevice, ObjectLogDevice, CheckpointDir) { }

    public abstract UnmanagedDictionary<TKey, TValue> Create();
}

public sealed class MemoryUnmanagedDictionaryStore<TKey, TValue> : UnmanagedDictionaryStore<TKey, TValue>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue> {

    internal MemoryUnmanagedDictionaryStore(long IndexSize, long PageSize, long SegmentSize, long MemorySize, double MutableFraction)
        : base(IndexSize, PageSize, SegmentSize, MemorySize, MutableFraction, new MemoryOnlyDevice(SegmentSize * 2), new MemoryOnlyDevice(Const.MinObjectLogCapacity), null) { }

    public override UnmanagedDictionary<TKey, TValue> Create() => new MemoryUnmanagedDictionary<TKey, TValue>(this);
}

public sealed class StorageUnmanagedDictionaryStore<TKey, TValue> : UnmanagedDictionaryStore<TKey, TValue>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue> {

    internal StorageUnmanagedDictionaryStore(long IndexSize, long PageSize, long SegmentSize, long MemorySize, double MutableFraction, IDevice LogDevice, IDevice ObjectLogDevice, string? CheckpointDir)
        : base(IndexSize, PageSize, SegmentSize, MemorySize, MutableFraction, LogDevice, ObjectLogDevice, CheckpointDir) { }

    public override UnmanagedDictionary<TKey, TValue> Create() => new StorageUnmanagedDictionary<TKey, TValue>(this);
}
