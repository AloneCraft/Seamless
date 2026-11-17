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
/// Tsavorite のハッシュセットのマルチ用の操作用オブジェクト。並列区間(<see cref="HashSetStore{TKey}.BeginParallel"/>)の中で、スレッドごとに作る。
/// 件数を管理しないので、件数の窓口(Count・ICollection・IReadOnlySet など)は持たない。件数は <see cref="ScanCount"/> が毎回全件を数える。
/// 全削除やまとめて書き換える集合演算は、シングル用の <see cref="HashSet{TKey}"/> を使う。
/// </summary>
[DebuggerDisplay("EntryCount={EntryCount}")]
public sealed class ParallelHashSet<TKey>:Collection<TKey,EqualityEmpty,global::Seamless.Collections.Serializers.Auto<TKey>,global::Seamless.Collections.Serializers.MemoryPack<EqualityEmpty>,ParallelAccess>, global::Seamless.Collections.IUnorderedEnumerable<TKey> {
    internal ParallelHashSet(CollectionStore<TKey,EqualityEmpty> collectionStore,ParallelRegionState region) : base(collectionStore,region) {
    }
    /// <summary>追加する。すでにあれば false。</summary>
    public bool Add(TKey item) {
        var result = this.PrivateTryAdd(item,EqualityEmpty.Default);
        switch(result) {
            case TryAddResult.Success:
                return true;
            case TryAddResult.KeyAlreadyExists:
                return false;
            default:
                Debug.Assert(result==TryAddResult.LogFull);
                throw this.ThrowLogFull();
        }
    }
    /// <summary>全件を数える(毎回全走査)。他のスレッドが書いている途中でも呼べるが、その時点の値ではない。</summary>
    public long ScanCount() => this.ScanCountCore();
    /// <summary>キーを 1 つずつ <typeparamref name="TStrategy"/> へ渡す。FLinq の生成コードが呼ぶ。</summary>
    public void Iterate<TStrategy>(ref TStrategy strategy)
        where TStrategy : struct, global::Seamless.Collections.Iteration.IStrategy<TStrategy,TKey> => this.IterateKeysCore(ref strategy);
    public Enumerator<KeyIterator,TKey> GetEnumerator() {
        this.VerifyThreadAffinity();
        return new(this.ReadSession,new(this.SerializationBudget),this.Store.Log.TailAddress,this);
    }
    IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator() => this.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    public override bool IsDisposed => this.disposed;
    private bool disposed;
    protected override void Dispose(bool disposing) {
        if(this.disposed)
            return;
        this.disposed=true;
        base.Dispose(disposing);
    }
}
