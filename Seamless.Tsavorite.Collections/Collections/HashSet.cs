namespace Seamless.Tsavorite.Collections;
/// <summary>
/// Tsavorite をバックエンドに使うオンメモリ HashSet。
/// <see cref="System.Collections.Generic.ISet{T}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 重複を許さず、キーのみを格納する。値は EqualityEmpty 構造体を使用する。
/// </summary>
[DebuggerTypeProxy(typeof(HashSetDebugView<>))]
public partial class HashSet<TKey>:Collection<TKey,EqualityEmpty,AutoSerializer<TKey>,MemoryPackSerializer<EqualityEmpty>>, IHashSet<TKey> {
    //private readonly byte[] EmptyBytes;
    private IDisposable? ownedStore;
    public bool IsReadOnly => false;
    public bool Add(TKey item) {
        if(this.Contains(item))
            return false;
        this.PrivateUpsert(item,EqualityEmpty.Default);
        return true;
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
        var workListPath = CreateTemporaryWorkListPath("hashset-intersect-ienumerable");
        try {
            using var keysToRemove = new Seamless.Persisted.Collections.SerializedList<TKey,AutoSerializer<TKey>>(workListPath,initialCapacity: Const.Chunk_Length,initialDataCapacity: 4096);
            foreach(var item in this) {
                if(!other.Contains(item))
                    keysToRemove.Add(item);
            }
            foreach(var item in keysToRemove)
                this.Remove(item);
        }
        finally {
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
        }
        else {
        }
        this.disposed=true;
        base.Dispose(disposing);
        if(disposing)
            this.ownedStore?.Dispose();
    }
}
