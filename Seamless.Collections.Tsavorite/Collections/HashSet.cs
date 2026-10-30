namespace Seamless.Collections.Tsavorite;

/// <summary>
/// Tsavorite のハッシュセット実装。
/// <see cref="System.Collections.Generic.ISet{T}"/>・<see cref="IReadOnlySet{T}"/>・<see cref="Seamless.Collections.ISet{T}"/> を実装し、
/// Tsavorite 永続ストア上で高速な追加・削除・検索を行うコレクションです。等価判定はシリアライズ後のバイト列の一致です。
/// </summary>
[DebuggerTypeProxy(typeof(HashSetDebugView<>))]
public partial class HashSet<TKey>:Collection<TKey,EqualityEmpty,global::Seamless.Collections.Serializers.Auto<TKey>,global::Seamless.Collections.Serializers.MemoryPack<EqualityEmpty>,ExclusiveAccess>, IHashSet<TKey>, global::System.Collections.Generic.ISet<TKey> {
    /// <summary>件数(O(1))。データ本体が管理している件数を返す。</summary>
    public int Count => checked((int)this.GetLongCountCore());
    /// <summary>件数(O(1))。データ本体が管理している件数を返す。</summary>
    public long GetLongCount() => this.GetLongCountCore();
    /// <summary>全件を削除する。</summary>
    public void Clear() => this.ClearCore();
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
            using var keysToRemove = new SerializedList<TKey,global::Seamless.Collections.Serializers.Auto<TKey>>(workListPath,initialCapacity: Const.Chunk_Length,initialDataCapacity: 4096);
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
        this.disposed=true;
        // 追加のセッションは基底の Dispose が DisposeAdditionalSessions で破棄する(ここで先に破棄すると二重になる)。
        try {
            base.Dispose(disposing);
        } finally {
            if(disposing)
                this.ownedStore?.Dispose();
        }
    }
}



public partial class HashSet<TKey> {
    /// <summary>キーを 1 つずつ <typeparamref name="TStrategy"/> へ渡す。FLinq の生成コードが呼ぶ。</summary>
    public void Iterate<TStrategy>(ref TStrategy strategy)
        where TStrategy : struct, global::Seamless.Collections.Iteration.IStrategy<TStrategy,TKey> => this.IterateKeysCore(ref strategy);
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
        try {
            this.CreateAdditionalSessions();
        } catch {
            this.AbortConstruction();
            throw;
        }
        // 構築に成功してから受け取る(失敗したときは呼び出し側が破棄する)。
        this.ownedStore=ownedStore;
    }
    private protected override void DisposeAdditionalSessions() {
        CompletePendingAndDispose(this.IntersectWith_ClientSession);
        CompletePendingAndDispose(this.SymmetricExceptWith_ClientSession);
    }
    private protected override void RecreateAdditionalSessions() {
        this.CreateAdditionalSessions();
    }
    private void CreateAdditionalSessions() {
        var Store = this.Store;
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
        base.ExceptWith(other);
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
