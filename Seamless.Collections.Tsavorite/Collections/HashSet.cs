namespace Seamless.Collections.Tsavorite;

/// <summary>
/// Tsavorite のハッシュセット実装。
/// <see cref="System.Collections.Generic.ISet{T}"/>・<see cref="IReadOnlySet{T}"/>・<see cref="Seamless.Collections.ISet{T}"/> を実装し、
/// Tsavorite 永続ストア上で高速な追加・削除・検索を行うコレクションです。等価判定はシリアライズ後のバイト列の一致です。
/// </summary>
[DebuggerTypeProxy(typeof(HashSetDebugView<>))]
public partial class HashSet<TKey>:Collection<TKey,EqualityEmpty,AutoSerializer<TKey>,MemoryPackSerializer<EqualityEmpty>>, IHashSet<TKey>, global::System.Collections.Generic.ISet<TKey> {
    // 関係演算は自分自身を全件メモリに載せず、逐次走査と Contains で求める(other が集合でなければ other だけを集合にする)。
    public bool IsProperSubsetOf(IEnumerable<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        return SetRelationHelpers.IsProperSubsetOf(this,other);
    }
    public bool IsProperSupersetOf(IEnumerable<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        return SetRelationHelpers.IsProperSupersetOf(this,other);
    }
    public bool IsSubsetOf(IEnumerable<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        return SetRelationHelpers.IsSubsetOf(this,other);
    }
    public bool IsSupersetOf(IEnumerable<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        return SetRelationHelpers.IsSupersetOf(this,other);
    }
    public bool Overlaps(IEnumerable<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        return SetRelationHelpers.Overlaps(this,other);
    }
    public bool SetEquals(IEnumerable<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        return SetRelationHelpers.SetEquals(this,other);
    }
    //private readonly byte[] EmptyBytes;
    private readonly IDisposable? ownedStore;
    public bool IsReadOnly => false;
    public bool Add(TKey item) {
        var result=this.PrivateTryAdd(item,EqualityEmpty.Default);
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
    void ICollection<TKey>.Add(TKey item) => this.Add(item);
    public void CopyTo(TKey[] array,int arrayIndex) {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
        if(array.Length-arrayIndex<this.GetLongCount())
            throw new ArgumentException("The destination array is too small.");
        foreach(var item in this) {
            array[arrayIndex++]=item;
        }
    }
    public partial void ExceptWith(HashSet<TKey> other);
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator() => this.GetEnumerator();
    public partial bool IsProperSubsetOf(HashSet<TKey> other);
    public partial bool IsProperSupersetOf(HashSet<TKey> other);
    public partial bool IsSubsetOf(HashSet<TKey> other);
    public partial bool IsSupersetOf(HashSet<TKey> other);
    public partial bool Overlaps(HashSet<TKey> other);
    public partial bool SetEquals(HashSet<TKey> other);
    public partial void IntersectWith(HashSet<TKey> other);
    public partial void SymmetricExceptWith(HashSet<TKey> other);
    public partial void UnionWith(HashSet<TKey> other);

    public void ExceptWith(Seamless.Collections.ISet<TKey> other) => TsavoriteSetOperations.ExceptWith(this,other);

    public void IntersectWith(Seamless.Collections.ISet<TKey> other) => TsavoriteSetOperations.IntersectWith(this,other);

    public bool IsSubsetOf(Seamless.Collections.ISet<TKey> other) => TsavoriteSetOperations.IsSubsetOf(this,other);

    public bool IsSupersetOf(Seamless.Collections.ISet<TKey> other) => TsavoriteSetOperations.IsSupersetOf(this,other);

    public bool IsProperSubsetOf(Seamless.Collections.ISet<TKey> other) => TsavoriteSetOperations.IsProperSubsetOf(this,other);

    public bool IsProperSupersetOf(Seamless.Collections.ISet<TKey> other) => TsavoriteSetOperations.IsProperSupersetOf(this,other);

    public bool Overlaps(Seamless.Collections.ISet<TKey> other) => TsavoriteSetOperations.Overlaps(this,other);

    public bool SetEquals(Seamless.Collections.ISet<TKey> other) => TsavoriteSetOperations.SetEquals(this,other);

    public void SymmetricExceptWith(Seamless.Collections.ISet<TKey> other) => TsavoriteSetOperations.SymmetricExceptWith(this,other);

    public void UnionWith(Seamless.Collections.ISet<TKey> other) => TsavoriteSetOperations.UnionWith(this,other);

    public void ExceptWith(global::System.Collections.Generic.ISet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in other)
            this.Remove(item);
    }

    public void ExceptWith(IEnumerable<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in other)
            this.Remove(item);
    }

    public void IntersectWith(global::System.Collections.Generic.ISet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var workListPath = this.CreateWorkListPath("hashset-intersect-ienumerable");
        using var quota = this.CreateWorkListQuota();
        try {
            using var keysToRemove = new SerializedList<TKey,AutoSerializer<TKey>>(workListPath,initialCapacity: Const.Chunk_Length,initialDataCapacity: 4096);
            keysToRemove.StorageQuota=quota;
            quota?.EnsureTotalBytes(((IPersistedCommittable)keysToRemove).StorageBytes);
            foreach(var item in this) {
                if(!other.Contains(item))
                    keysToRemove.Add(item);
            }
            foreach(var item in keysToRemove)
                this.Remove(item);
        } finally {
            DeleteTemporaryWorkListPath(workListPath);
        }
    }

    public void IntersectWith(IEnumerable<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        if(other is global::System.Collections.Generic.ISet<TKey> set) {
            this.IntersectWith(set);
            return;
        }
        var materialized = new global::System.Collections.Generic.HashSet<TKey>(other);
        this.IntersectWith(materialized);
    }

    public void SymmetricExceptWith(global::System.Collections.Generic.ISet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in other) {
            if(!this.Remove(item))
                this.Add(item);
        }
    }

    public void SymmetricExceptWith(IEnumerable<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        if(other is global::System.Collections.Generic.ISet<TKey> set) {
            this.SymmetricExceptWith(set);
            return;
        }
        var materialized = new global::System.Collections.Generic.HashSet<TKey>(other);
        this.SymmetricExceptWith(materialized);
    }

    public void UnionWith(global::System.Collections.Generic.ISet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in other)
            this.Add(item);
    }

    public void UnionWith(IEnumerable<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in other)
            this.Add(item);
    }
    public override bool IsDisposed => this.disposed;
    private bool disposed;
    protected override void Dispose(bool disposing) {
        if(this.disposed) return;
        if(disposing) {
            this.IntersectWith_ClientSession.Dispose();
            this.SymmetricExceptWith_ClientSession.Dispose();
        } else {
        }
        this.disposed=true;
        base.Dispose(disposing);
        if(disposing)
            this.ownedStore?.Dispose();
    }
}



public interface IHashSetIterateStrategy<TSelf, T>
    where TSelf : struct, IHashSetIterateStrategy<TSelf,T> {
    static abstract bool Invoke(ref TSelf self,T item);
}

public partial class HashSet<TKey> {
    // Reader から TStrategy.Invoke を直接呼ぶ push 型実装は、通常の列挙経路
    // (Enumerator<KeyIterator,TKey>、ArrayPool チャンクへ Accept で詰めてから配る pull 型)より
    // 実測で 3〜4 倍遅かった(Test.Tsavorite.Collections.NativeIterateVsEnumeratorDiagnostic で検証)。
    // 通常経路と同じ IterateLookupIteratorFunctions_List<TKey>(sealed 非仮想 Reader・ArrayPool チャンク)
    // へ揃えることで解消する。チャンクに溜めてから Invoke を回す都合上、Any/First 等の早期終了は
    // 現在のチャンク(既定 4096 要素)を読み切ってから止まる — 全走査系(Count/ToList/Sum 等)には無関係。
    public void Iterate<TStrategy>(ref TStrategy strategy)
        where TStrategy : struct, IHashSetIterateStrategy<TStrategy,TKey> {
        this.VerifyThreadAffinity();
        var iterator = new IterateLookupAdapter();
        var cursor = 0L;
        try {
            while(true) {
                var mayHaveMore = this.ReadSession.IterateLookup(ref iterator,ref cursor,resetCursor: false);
                this.ReadSession.BasicContext.CompletePending(wait: true);
                for(var index = 0;index<iterator.Count;index++) {
                    if(!TStrategy.Invoke(ref strategy,iterator.Chunk[index])) {
                        return;
                    }
                }
                if(!mayHaveMore) {
                    return;
                }
                iterator.ResetChunk();
            }
        } finally {
            iterator.ReturnChunk();
        }
    }

    private sealed class IterateLookupAdapter:IterateLookupIteratorFunctions_List<TKey> {
        public override bool Reader<TSourceLogRecord>(
            in TSourceLogRecord logRecord,
            RecordMetadata recordMetadata,
            long numberOfRecords,
            out CursorRecordResult cursorRecordResult) {
            if(logRecord.Info.Tombstone) {
                return Skip(out cursorRecordResult);
            }
            return this.Accept(AutoSerializer<TKey>.Deserialize(logRecord.Key),recordMetadata,out cursorRecordResult);
        }
    }
}


/// <summary>
/// Tsavorite の HashSet スキャン操作向けの内部ヘルパークラス。
/// <see cref="System.Collections.Generic.ISet{T}"/> の走査を非ジェネリック経由で実行するための
/// 補助メソッドを提供します。
/// </summary>
[DebuggerTypeProxy(typeof(HashSetDebugView<>))]
public partial class HashSet<TKey> {
    private ClientSession<
        FixedSpanByteKey,//TKey
        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        ExceptWith,//TFunctions
        SpanByteStoreFunctions,
        SpanByteStoreAllocator
    > ExceptWith_ClientSession = null!;
    private ClientSession<
        FixedSpanByteKey,//TKey
        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        IntersectWith,//TFunctions
        SpanByteStoreFunctions,//TStoreFunctions
        SpanByteStoreAllocator
    > IntersectWith_ClientSession = null!;
    private ClientSession<
        FixedSpanByteKey,//TKey
        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        SymmetricExceptWith,//TFunctions
        SpanByteStoreFunctions,
        SpanByteStoreAllocator
    > SymmetricExceptWith_ClientSession = null!;
    internal HashSet(CollectionStore<TKey,EqualityEmpty> CollectionStore,IDisposable? ownedStore = null) : base(CollectionStore) {
        this.ownedStore=ownedStore;
        this.CreateAdditionalSessions();
    }
    private protected override void DisposeAdditionalSessions() {
        CompletePendingAndDispose(this.ExceptWith_ClientSession);
        CompletePendingAndDispose(this.IntersectWith_ClientSession);
        CompletePendingAndDispose(this.SymmetricExceptWith_ClientSession);
    }
    private protected override void RecreateAdditionalSessions() {
        this.CreateAdditionalSessions();
    }
    private void CreateAdditionalSessions() {
        var Store = this.Store;
        this.ExceptWith_ClientSession=Store.NewSession<
            FixedSpanByteKey,//TKey
            EqualityEmpty,//TInput
            EqualityEmpty,//TOutput
            EqualityEmpty,//TContext
            ExceptWith//TFunctions
        >(new());
        this.IntersectWith_ClientSession=Store.NewSession<
            FixedSpanByteKey,//TKey
            EqualityEmpty,//TInput
            EqualityEmpty,//TOutput
            EqualityEmpty,//TContext
            IntersectWith//TFunctions
        >(new());
        this.SymmetricExceptWith_ClientSession=Store.NewSession<
            FixedSpanByteKey,//TKey
            EqualityEmpty,//TInput
            EqualityEmpty,//TOutput
            EqualityEmpty,//TContext
            SymmetricExceptWith//TFunctions
        >(new());
    }
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    public struct ValueEnumerator {
        private HashSet<TKey>? source;
        private Enumerator<KeyIterator,TKey> enumerator;
        private bool initialized;

        internal ValueEnumerator(HashSet<TKey> source) {
            this.source=source;
            this.enumerator=default;
            this.initialized=false;
        }

        public bool TryGetNext(out TKey current) {
            if(!this.initialized) {
                this.enumerator=this.source!.GetEnumerator();
                this.initialized=true;
            }
            if(this.enumerator.MoveNext()) {
                current=this.enumerator.Current;
                return true;
            }
            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            if(this.initialized) {
                this.enumerator.Dispose();
            }
            this.source=null;
        }
    }

    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    public ValueEnumerator GetValueEnumerator() => new(this);
    public Enumerator<
        KeyIterator,
        TKey
    > GetEnumerator() {
        this.VerifyThreadAffinity();
        return new(this.ReadSession,new(),this.Store.Log.TailAddress,this);
    }
    IEnumerator<TKey> IMemoryCollection<TKey>.GetEnumerator() {
        this.VerifyThreadAffinity();
        return new Enumerator<MemoryKeyIterator,TKey>(this.ReadSession,new(),this.Store.Log.TailAddress,this);
    }
    public partial void ExceptWith(HashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        base.ExceptWith(other,this.ExceptWith_ClientSession);
    }
    public partial void IntersectWith(HashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        this.IntersectWith(this,other,this.IntersectWith_ClientSession);
    }
    public partial bool IsProperSubsetOf(HashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        return this.Count<other.Count&&this.IsSubsetOf(other);
    }
    public partial bool IsProperSupersetOf(HashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        return this.Count>other.Count&&this.IsSupersetOf(other);
    }
    public partial bool IsSubsetOf(HashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in this) {
            if(!other.Contains(item))
                return false;
        }
        return true;
    }
    public partial bool IsSupersetOf(HashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        return other.IsSubsetOf(this);
    }
    public partial bool Overlaps(HashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in this) {
            if(other.Contains(item))
                return true;
        }
        return false;
    }
    public partial bool SetEquals(HashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        return this.Count==other.Count&&this.IsSubsetOf(other);
    }
    public partial void SymmetricExceptWith(HashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        base.SymmetricExceptWith(other,this.SymmetricExceptWith_ClientSession);
    }
    public partial void UnionWith(HashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        base.UnionWith(other,this.Add_ClientSession);
    }
}
