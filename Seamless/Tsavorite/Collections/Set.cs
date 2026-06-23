using Seamless.Tsavorite.Stores;
namespace Seamless.Tsavorite.Collections;

public class Set<TKey, TRecord>:Collection<TKey,TRecord,AutoSerializer<TKey>,AutoSerializer<TRecord>>,ISet<TKey,TRecord>, IMemoryCollection<TRecord>
    where TRecord : IRecord<TKey> {
    private readonly IDisposable? ownedStore;
    internal Set(CollectionStore<TKey,TRecord> collectionStore,IDisposable? ownedStore = null) : base(collectionStore) {
        this.ownedStore=ownedStore;
    }
    public bool IsReadOnly => false;
    public bool Add(TRecord item) {
        var result = PrivateTryAdd(item.Key,item);
        switch(result) {
            case TryAddResult.Success:
                return true;
            case TryAddResult.KeyAlreadyExists:
                return false;
            default:
                Debug.Assert(result==TryAddResult.LogFull);
                throw ThrowLogFull();
        }
    }
    void ICollection<TRecord>.Add(TRecord item) => Add(item);
    public bool Contains(TRecord item) => ContainsKey(item.Key);
    public new bool ContainsKey(TKey key) => base.ContainsKey(key);
    public bool Remove(TRecord item) => Remove(item.Key);
    public bool Remove(TKey key,[MaybeNullWhen(false)] out TRecord value) {
        if(!TryGetValue(key,out value)) {
            value=default;
            return false;
        }
        if(Remove(key))
            return true;
        value=default;
        return false;
    }
    public void CopyTo(TRecord[] array,int arrayIndex) {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
        if(array.Length-arrayIndex<GetLongCount())
            throw new ArgumentException("The destination array is too small.");
        foreach(var item in this)
            array[arrayIndex++]=item;
    }
    public void ExceptWith(ISet<TKey,TRecord> other) => TsavoriteSetOperations.ExceptWith(this,other);
    public void ExceptWith(Set<TKey,TRecord> other) => ExceptWith((ISet<TKey,TRecord>)other);
    public void IntersectWith(ISet<TKey,TRecord> other) => TsavoriteSetOperations.IntersectWith(this,other);
    public void IntersectWith(Set<TKey,TRecord> other) => IntersectWith((ISet<TKey,TRecord>)other);
    public bool IsSubsetOf(ISet<TKey,TRecord> other) => TsavoriteSetOperations.IsSubsetOf(this,other);
    public bool IsSubsetOf(Set<TKey,TRecord> other) => IsSubsetOf((ISet<TKey,TRecord>)other);
    public bool IsSupersetOf(ISet<TKey,TRecord> other) => TsavoriteSetOperations.IsSupersetOf(this,other);
    public bool IsSupersetOf(Set<TKey,TRecord> other) => IsSupersetOf((ISet<TKey,TRecord>)other);
    public bool IsProperSubsetOf(ISet<TKey,TRecord> other) => TsavoriteSetOperations.IsProperSubsetOf(this,other);
    public bool IsProperSubsetOf(Set<TKey,TRecord> other) => IsProperSubsetOf((ISet<TKey,TRecord>)other);
    public bool IsProperSupersetOf(ISet<TKey,TRecord> other) => TsavoriteSetOperations.IsProperSupersetOf(this,other);
    public bool IsProperSupersetOf(Set<TKey,TRecord> other) => IsProperSupersetOf((ISet<TKey,TRecord>)other);
    public bool Overlaps(ISet<TKey,TRecord> other) => TsavoriteSetOperations.Overlaps(this,other);
    public bool Overlaps(Set<TKey,TRecord> other) => Overlaps((ISet<TKey,TRecord>)other);
    public bool SetEquals(ISet<TKey,TRecord> other) => TsavoriteSetOperations.SetEquals(this,other);
    public bool SetEquals(Set<TKey,TRecord> other) => SetEquals((ISet<TKey,TRecord>)other);
    public void SymmetricExceptWith(ISet<TKey,TRecord> other) => TsavoriteSetOperations.SymmetricExceptWith(this,other);
    public void SymmetricExceptWith(Set<TKey,TRecord> other) => SymmetricExceptWith((ISet<TKey,TRecord>)other);
    public void UnionWith(ISet<TKey,TRecord> other) => TsavoriteSetOperations.UnionWith(this,other);
    public void UnionWith(Set<TKey,TRecord> other) => UnionWith((ISet<TKey,TRecord>)other);

    public Set<TKey,TRecord> Except(Set<TKey,TRecord> other) => Except((ISet<TKey,TRecord>)other);
    public Set<TKey,TRecord> Except(ISet<TKey,TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedResultSet();
        foreach(var item in this) {
            if(!other.ContainsKey(item.Key))
                result.Add(item);
        }
        return result;
    }
    public HashSet<TKey> Except(HashSet<TKey> other) => Except((IHashSet<TKey>)other);
    public HashSet<TKey> Except(IHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedKeyResultSet();
        foreach(var item in this) {
            if(!((ICollection<TKey>)other).Contains(item.Key))
                result.Add(item.Key);
        }
        return result;
    }

    public Set<TKey,TRecord> Intersect(Set<TKey,TRecord> other) => Intersect((ISet<TKey,TRecord>)other);
    public Set<TKey,TRecord> Intersect(ISet<TKey,TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedResultSet();
        foreach(var item in this) {
            if(other.ContainsKey(item.Key))
                result.Add(item);
        }
        return result;
    }
    public HashSet<TKey> Intersect(HashSet<TKey> other) => Intersect((IHashSet<TKey>)other);
    public HashSet<TKey> Intersect(IHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedKeyResultSet();
        foreach(var item in this) {
            if(((ICollection<TKey>)other).Contains(item.Key))
                result.Add(item.Key);
        }
        return result;
    }

    public Set<TKey,TRecord> SymmetricExcept(Set<TKey,TRecord> other) => SymmetricExcept((ISet<TKey,TRecord>)other);
    public Set<TKey,TRecord> SymmetricExcept(ISet<TKey,TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedResultSet();
        foreach(var item in this) {
            if(!other.ContainsKey(item.Key))
                result.Add(item);
        }
        foreach(var item in other) {
            if(!ContainsKey(item.Key))
                result.Add(item);
        }
        return result;
    }
    public HashSet<TKey> SymmetricExcept(HashSet<TKey> other) => SymmetricExcept((IHashSet<TKey>)other);
    public HashSet<TKey> SymmetricExcept(IHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedKeyResultSet();
        foreach(var item in this) {
            if(!((ICollection<TKey>)other).Contains(item.Key))
                result.Add(item.Key);
        }
        foreach(var key in other) {
            if(!ContainsKey(key))
                result.Add(key);
        }
        return result;
    }

    public Set<TKey,TRecord> Union(Set<TKey,TRecord> other) => Union((ISet<TKey,TRecord>)other);
    public Set<TKey,TRecord> Union(ISet<TKey,TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedResultSet();
        foreach(var item in this)
            result.Add(item);
        foreach(var item in other)
            result.Add(item);
        return result;
    }
    public HashSet<TKey> Union(HashSet<TKey> other) => Union((IHashSet<TKey>)other);
    public HashSet<TKey> Union(IHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedKeyResultSet();
        foreach(var item in this)
            result.Add(item.Key);
        foreach(var key in other)
            result.Add(key);
        return result;
    }

    private static Set<TKey,TRecord> CreateOwnedResultSet() {
        var store = global::Seamless.Tsavorite.Collections.Store.Create().CreateSet<TKey,TRecord>();
        return new(store,store);
    }

    private static HashSet<TKey> CreateOwnedKeyResultSet() {
        var store = global::Seamless.Tsavorite.Collections.Store.Create().Create<TKey>();
        return store.CreateOwned();
    }

    public override bool IsDisposed => disposed;
    private bool disposed;
    protected override void Dispose(bool disposing) {
        if(disposed)
            return;
        disposed=true;
        base.Dispose(disposing);
        if(disposing)
            ownedStore?.Dispose();
    }
    public IEnumerator<TRecord> GetEnumerator() => new Enumerator<ValueIterator,TRecord>(ReadSession,new(),Store.Log.TailAddress);
    IEnumerator<TRecord> IEnumerable<TRecord>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    IEnumerator<TRecord> IMemoryCollection<TRecord>.GetEnumerator() => new Enumerator<MemoryValueIterator,TRecord>(ReadSession,new(),Store.Log.TailAddress);
}




public class Set<TRecord> : HashSet<TRecord> {
    internal Set(CollectionStore<TRecord,EqualityEmpty> collectionStore,IDisposable? ownedStore = null) : base(collectionStore,ownedStore) {
    }
}
