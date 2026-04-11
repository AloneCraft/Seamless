namespace Seamless.Collections;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Tsavorite.core;
using Seamless.Devices;

public abstract class StructCollectionStore<TKey, TValue> : IDisposable
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue>
{
#if TSAVORITE2_0BETA3
    private protected KVSettings Settings { get; private set; }
#elif TSAVORITE1_1_1
    private protected KVSettings<TKey, TValue> Settings { get; private set; }
#endif

    public readonly TsavoriteKV<TKey, TValue> Store;

    private protected StructCollectionStore(long IndexSize, long PageSize, long SegmentSize, long MemorySize, double MutableFraction, IDevice LogDevice, IDevice ObjectLogDevice, string? CheckpointDir)
    {
#if TSAVORITE2_0BETA3
        this.Settings = new KVSettings {
            IndexSize = IndexSize,
            LogDevice = LogDevice,
            ObjectLogDevice = ObjectLogDevice,
            PageSize = PageSize,
            MemorySize = MemorySize,
            SegmentSize = SegmentSize
        };
        if (CheckpointDir != null) this.Settings.CheckpointDir = CheckpointDir;
        this.Store = new TsavoriteKV<TKey, TValue>(this.Settings);
#elif TSAVORITE1_1_1
        this.Settings = new KVSettings<TKey, TValue> {
            IndexSize = IndexSize,
            LogDevice = LogDevice,
            ObjectLogDevice = ObjectLogDevice,
            PageSize = PageSize,
            MemorySize = MemorySize,
            SegmentSize = SegmentSize
        };
        if (CheckpointDir != null) this.Settings.CheckpointDir = CheckpointDir;
        this.Store = new TsavoriteKV<TKey, TValue>(this.Settings);
#endif
    }

    public void Dispose() => this.Store.Dispose();
}

public abstract class StructDictionaryStore<TKey, TValue> : StructCollectionStore<TKey, TValue>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue>
{
    private protected StructDictionaryStore(long IndexSize, long PageSize, long SegmentSize, long MemorySize, double MutableFraction, IDevice LogDevice, IDevice ObjectLogDevice, string? CheckpointDir) :
        base(IndexSize, PageSize, SegmentSize, MemorySize, MutableFraction, LogDevice, ObjectLogDevice, CheckpointDir) { }

    public abstract StructDictionary<TKey, TValue> Create();
}

public sealed class StructMemoryDictionaryStore<TKey, TValue> : StructDictionaryStore<TKey, TValue>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue>
{
    public StructMemoryDictionaryStore() :
        base(1L << 20, 1L << 24, 1L << 26, 1L << 28, 0.9, new NullDevice(), new NullDevice(), null) { }

    public override StructDictionary<TKey, TValue> Create() => new StructMemoryDictionary<TKey, TValue>(this);
}

public abstract class StructDictionary<TKey, TValue>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue>
{
    internal readonly ClientSession<TKey, TValue, TValue, TValue, Empty, SimpleFunctions<TKey, TValue>> ClientSession;

    internal StructDictionary(StructCollectionStore<TKey, TValue> store)
    {
        this.ClientSession = store.Store.NewSession<TValue, TValue, Empty, SimpleFunctions<TKey, TValue>>(new SimpleFunctions<TKey, TValue>());
    }

    public void Add(TKey key, TValue value)
    {
        this.ClientSession.Upsert(ref key, ref value);
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        var input = default(TValue);
        value = default;
        var status = this.ClientSession.Read(ref key, ref input, ref value);
        if (status.IsPending)
        {
            this.ClientSession.CompletePendingWithOutputs(out var outputs, wait: true);
            while (outputs.Next())
            {
                if (outputs.Current.Status.Found)
                {
                    value = outputs.Current.Output;
                    return true;
                }
            }
            return false;
        }
        return status.Found;
    }

    public bool Remove(TKey key)
    {
        var status = this.ClientSession.Delete(ref key);
        if (status.IsPending)
        {
            this.ClientSession.CompletePending(wait: true);
            return true; 
        }
        return true; 
    }
}

public sealed class StructMemoryDictionary<TKey, TValue> : StructDictionary<TKey, TValue>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue>
{
    internal StructMemoryDictionary(StructMemoryDictionaryStore<TKey, TValue> store) : base(store) { }
}
namespace Seamless.Collections;

using System;
using System.Collections;
using System.Collections.Generic;
using Tsavorite.core;
using Seamless.Devices;

public partial class StructDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue>
{
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        using var iter = this.ClientSession.Iterate();
        while (iter.GetNext(out var refRecord))
        {
            yield return new KeyValuePair<TKey, TValue>(refRecord.GetKey(), refRecord.GetValue());
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
}
