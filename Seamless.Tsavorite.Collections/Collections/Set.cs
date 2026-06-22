using Seamless.Tsavorite.Collections.Stores;

namespace Seamless.Tsavorite.Collections;

public class Set<TKey, TRecord>:Collection<TKey,TRecord,AutoSerializer<TKey>,AutoSerializer<TRecord>>,Seamless.Collections.ISet<TKey,TRecord>, IMemoryCollection<TRecord>
    where TRecord : Seamless.Collections.IRecord<TKey> {
    private readonly IDisposable? ownedStore;
    internal Set(CollectionStore<TKey,TRecord> collectionStore,IDisposable? ownedStore = null) : base(collectionStore) {
        this.ownedStore=ownedStore;
    }
    public bool IsReadOnly => false;
    public bool Add(TRecord item) {
        var result = this.PrivateTryAdd(item.Key,item);
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
    void ICollection<TRecord>.Add(TRecord item) => this.Add(item);
    public bool Contains(TRecord item) => this.ContainsKey(item.Key);
    public new bool ContainsKey(TKey key) => base.ContainsKey(key);
    public bool Remove(TRecord item) => this.Remove(item.Key);
    public bool Remove(TKey key,[MaybeNullWhen(false)] out TRecord value) {
        if(!this.TryGetValue(key,out value)) {
            value=default;
            return false;
        }
        if(this.Remove(key))
            return true;
        value=default;
        return false;
    }
    public void CopyTo(TRecord[] array,int arrayIndex) {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
        if(array.Length-arrayIndex<this.GetLongCount())
            throw new ArgumentException("The destination array is too small.");
        foreach(var item in this)
            array[arrayIndex++]=item;
    }
    public void ExceptWith(Seamless.Collections.ISet<TKey,TRecord> other) => TsavoriteSetOperations.ExceptWith(this,other);
    public void ExceptWith(Set<TKey,TRecord> other) => this.ExceptWith((Seamless.Collections.ISet<TKey,TRecord>)other);
    public void IntersectWith(Seamless.Collections.ISet<TKey,TRecord> other) => TsavoriteSetOperations.IntersectWith(this,other);
    public void IntersectWith(Set<TKey,TRecord> other) => this.IntersectWith((Seamless.Collections.ISet<TKey,TRecord>)other);
    public bool IsSubsetOf(Seamless.Collections.ISet<TKey,TRecord> other) => TsavoriteSetOperations.IsSubsetOf(this,other);
    public bool IsSubsetOf(Set<TKey,TRecord> other) => this.IsSubsetOf((Seamless.Collections.ISet<TKey,TRecord>)other);
    public bool IsSupersetOf(Seamless.Collections.ISet<TKey,TRecord> other) => TsavoriteSetOperations.IsSupersetOf(this,other);
    public bool IsSupersetOf(Set<TKey,TRecord> other) => this.IsSupersetOf((Seamless.Collections.ISet<TKey,TRecord>)other);
    public bool IsProperSubsetOf(Seamless.Collections.ISet<TKey,TRecord> other) => TsavoriteSetOperations.IsProperSubsetOf(this,other);
    public bool IsProperSubsetOf(Set<TKey,TRecord> other) => this.IsProperSubsetOf((Seamless.Collections.ISet<TKey,TRecord>)other);
    public bool IsProperSupersetOf(Seamless.Collections.ISet<TKey,TRecord> other) => TsavoriteSetOperations.IsProperSupersetOf(this,other);
    public bool IsProperSupersetOf(Set<TKey,TRecord> other) => this.IsProperSupersetOf((Seamless.Collections.ISet<TKey,TRecord>)other);
    public bool Overlaps(Seamless.Collections.ISet<TKey,TRecord> other) => TsavoriteSetOperations.Overlaps(this,other);
    public bool Overlaps(Set<TKey,TRecord> other) => this.Overlaps((Seamless.Collections.ISet<TKey,TRecord>)other);
    public bool SetEquals(Seamless.Collections.ISet<TKey,TRecord> other) => TsavoriteSetOperations.SetEquals(this,other);
    public bool SetEquals(Set<TKey,TRecord> other) => this.SetEquals((Seamless.Collections.ISet<TKey,TRecord>)other);
    public void SymmetricExceptWith(Seamless.Collections.ISet<TKey,TRecord> other) => TsavoriteSetOperations.SymmetricExceptWith(this,other);
    public void SymmetricExceptWith(Set<TKey,TRecord> other) => this.SymmetricExceptWith((Seamless.Collections.ISet<TKey,TRecord>)other);
    public void UnionWith(Seamless.Collections.ISet<TKey,TRecord> other) => TsavoriteSetOperations.UnionWith(this,other);
    public void UnionWith(Set<TKey,TRecord> other) => this.UnionWith((Seamless.Collections.ISet<TKey,TRecord>)other);

    public Set<TKey,TRecord> Except(Set<TKey,TRecord> other) => (Set<TKey,TRecord>)this.Except((Seamless.Collections.ISet<TKey,TRecord>)other);
    public Seamless.Collections.ISet<TKey,TRecord> Except(Seamless.Collections.ISet<TKey,TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedResultSet();
        foreach(var item in this) {
            if(!other.ContainsKey(item.Key))
                result.Add(item);
        }
        return result;
    }
    public HashSet<TKey> Except(HashSet<TKey> other) => (HashSet<TKey>)this.Except((IHashSet<TKey>)other);
    public IHashSet<TKey> Except(IHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedKeyResultSet();
        foreach(var item in this) {
            if(!((ICollection<TKey>)other).Contains(item.Key))
                result.Add(item.Key);
        }
        return result;
    }

    public Set<TKey,TRecord> Intersect(Set<TKey,TRecord> other) => (Set<TKey,TRecord>)this.Intersect((Seamless.Collections.ISet<TKey,TRecord>)other);
    public Seamless.Collections.ISet<TKey,TRecord> Intersect(Seamless.Collections.ISet<TKey,TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedResultSet();
        foreach(var item in this) {
            if(other.ContainsKey(item.Key))
                result.Add(item);
        }
        return result;
    }
    public HashSet<TKey> Intersect(HashSet<TKey> other) => (HashSet<TKey>)this.Intersect((IHashSet<TKey>)other);
    public IHashSet<TKey> Intersect(IHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedKeyResultSet();
        foreach(var item in this) {
            if(((ICollection<TKey>)other).Contains(item.Key))
                result.Add(item.Key);
        }
        return result;
    }

    public Set<TKey,TRecord> SymmetricExcept(Set<TKey,TRecord> other) => (Set<TKey,TRecord>)this.SymmetricExcept((Seamless.Collections.ISet<TKey,TRecord>)other);
    public Seamless.Collections.ISet<TKey,TRecord> SymmetricExcept(Seamless.Collections.ISet<TKey,TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedResultSet();
        foreach(var item in this) {
            if(!other.ContainsKey(item.Key))
                result.Add(item);
        }
        foreach(var item in other) {
            if(!this.ContainsKey(item.Key))
                result.Add(item);
        }
        return result;
    }
    public HashSet<TKey> SymmetricExcept(HashSet<TKey> other) => (HashSet<TKey>)this.SymmetricExcept((IHashSet<TKey>)other);
    public IHashSet<TKey> SymmetricExcept(IHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedKeyResultSet();
        foreach(var item in this) {
            if(!((ICollection<TKey>)other).Contains(item.Key))
                result.Add(item.Key);
        }
        foreach(var key in other) {
            if(!this.ContainsKey(key))
                result.Add(key);
        }
        return result;
    }

    public Set<TKey,TRecord> Union(Set<TKey,TRecord> other) => (Set<TKey,TRecord>)this.Union((Seamless.Collections.ISet<TKey,TRecord>)other);
    public Seamless.Collections.ISet<TKey,TRecord> Union(Seamless.Collections.ISet<TKey,TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedResultSet();
        foreach(var item in this)
            result.Add(item);
        foreach(var item in other)
            result.Add(item);
        return result;
    }
    public HashSet<TKey> Union(HashSet<TKey> other) => (HashSet<TKey>)this.Union((IHashSet<TKey>)other);
    public IHashSet<TKey> Union(IHashSet<TKey> other) {
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

    public override bool IsDisposed => this.disposed;
    private bool disposed;
    protected override void Dispose(bool disposing) {
        if(this.disposed)
            return;
        this.disposed=true;
        base.Dispose(disposing);
        if(disposing)
            this.ownedStore?.Dispose();
    }
    public IEnumerator<TRecord> GetEnumerator() => new Enumerator<ValueIterator,TRecord>(this.ReadSession,new(),this.Store.Log.TailAddress);
    IEnumerator<TRecord> IEnumerable<TRecord>.GetEnumerator() => this.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    IEnumerator<TRecord> IMemoryCollection<TRecord>.GetEnumerator() => new Enumerator<MemoryValueIterator,TRecord>(this.ReadSession,new(),this.Store.Log.TailAddress);
}
