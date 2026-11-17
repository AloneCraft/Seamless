using EqualityEmpty = Seamless.Collections.Tsavorite.EqualityEmpty;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SpanByteStoreAllocator = Tsavorite.core.SpanByteAllocator<
    Tsavorite.core.StoreFunctions<
        Tsavorite.core.SpanByteComparer,
        Tsavorite.core.DefaultRecordTriggers
    >
>;
using SpanByteStoreFunctions = Tsavorite.core.StoreFunctions<
    Tsavorite.core.SpanByteComparer,
    Tsavorite.core.DefaultRecordTriggers
>;
using Garnet.common;
using Seamless.Collections;
using Tsavorite.core;
namespace Seamless.Collections.Tsavorite;
/// <summary>
/// これはSystem.Collections.Generic.List<T>に使えるならそれに置き換えてこれは削除して
/// </summary>
/// <typeparam name="T"></typeparam>
public sealed class InMemoryList<T>:PersistedList<T> {
    private readonly List<T> items;
    private StorageTuning tuning = StorageTuning.Default;

    public override StorageTuning Tuning => this.tuning;

    public InMemoryList(long initialCapacity = 16) {
        if(initialCapacity<1)
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        if(initialCapacity>int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));

        this.items=new(checked((int)initialCapacity));
        this.Capacity=this.items.Capacity;
    }

    public override void Add(T item) {
        this.ThrowIfDisposed();

        this.items.Add(item);
        this.Count=this.items.Count;
        this.Capacity=this.items.Capacity;
    }

    public override void SetTuning(StorageTuning tuning) {
        this.ThrowIfDisposed();
        this.tuning=tuning;
    }

    public new ref T this[int index] => ref this[checked((long)index)];

    public new ref T this[long index] {
        get {
            this.ThrowIfDisposed();
            if((ulong)index>=(ulong)this.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return ref CollectionsMarshal.AsSpan(this.items)[checked((int)index)];
        }
    }

    public override ReadOnlySpan<T> AsSpan(long start,int length) {
        this.ThrowIfDisposed();
        this.ValidateSpanRange(start,length);
        return CollectionsMarshal.AsSpan(this.items).Slice(checked((int)start),length);
    }

    protected override T GetItem(long index) =>
        this.items[checked((int)index)];

    protected override void OnCountChanged() {
        var targetCount = checked((int)this.Count);
        if(targetCount<this.items.Count) {
            this.items.RemoveRange(targetCount,this.items.Count-targetCount);
        }
        this.Capacity=Math.Max(this.Capacity,this.items.Capacity);
    }

    protected override void DisposeCore() {
        this.items.Clear();
        this.Count=0;
    }
}
