using Seamless.Stores;

namespace Seamless.Collections;

public class Set<TRecord>:List<TRecord>, ISet<TRecord> {

    public Set(string path,long initialCapacity = 16,long initialDataCapacity = 4096) :
        base(path,initialCapacity,initialDataCapacity) {
    }

    public Set<TRecord> Distinct() {
        this.DuplicateRemoval();
        return this;
    }
    public void Clear() {
        throw new NotImplementedException();
    }
    public bool Contains(TRecord item) {
        throw new NotImplementedException();
    }
    public void CopyTo(TRecord[] array,int arrayIndex) {
        if(array.Length-arrayIndex<this.GetLongCount())
            throw new ArgumentException("The destination array is too small.");
        foreach(var item in this)
            array[arrayIndex++]=item;
    }
    public bool Remove(TRecord item) {
        throw new NotImplementedException();
    }
    public new int Count => checked((int)this.GetLongCount());
    public bool IsReadOnly => false;
    public long EntryCount => this.GetLongCount();
    public IEnumerator<TRecord> GetEnumerator() {
        throw new NotImplementedException();
    }
    IEnumerator<TRecord> IEnumerable<TRecord>.GetEnumerator() => this.GetEnumerator();
}
public partial class Set<TKey, TRecord>:
    Collection<TKey,TRecord,AutoSerializer<TKey>,AutoSerializer<TRecord>>,
    ISeamlessSet<TKey,TRecord>
    where TRecord : IRecord<TKey> {
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
    public void ExceptWith(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.ExceptWith(this,other);
    public void ExceptWith(Set<TKey,TRecord> other) => this.ExceptWith((ISeamlessSet<TKey,TRecord>)other);
    public void IntersectWith(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.IntersectWith(this,other);
    public void IntersectWith(Set<TKey,TRecord> other) => this.IntersectWith((ISeamlessSet<TKey,TRecord>)other);
    public bool IsSubsetOf(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.IsSubsetOf(this,other);
    public bool IsSubsetOf(Set<TKey,TRecord> other) => this.IsSubsetOf((ISeamlessSet<TKey,TRecord>)other);
    public bool IsSupersetOf(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.IsSupersetOf(this,other);
    public bool IsSupersetOf(Set<TKey,TRecord> other) => this.IsSupersetOf((ISeamlessSet<TKey,TRecord>)other);
    public bool IsProperSubsetOf(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.IsProperSubsetOf(this,other);
    public bool IsProperSubsetOf(Set<TKey,TRecord> other) => this.IsProperSubsetOf((ISeamlessSet<TKey,TRecord>)other);
    public bool IsProperSupersetOf(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.IsProperSupersetOf(this,other);
    public bool IsProperSupersetOf(Set<TKey,TRecord> other) => this.IsProperSupersetOf((ISeamlessSet<TKey,TRecord>)other);
    public bool Overlaps(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.Overlaps(this,other);
    public bool Overlaps(Set<TKey,TRecord> other) => this.Overlaps((ISeamlessSet<TKey,TRecord>)other);
    public bool SetEquals(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.SetEquals(this,other);
    public bool SetEquals(Set<TKey,TRecord> other) => this.SetEquals((ISeamlessSet<TKey,TRecord>)other);
    public void SymmetricExceptWith(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.SymmetricExceptWith(this,other);
    public void SymmetricExceptWith(Set<TKey,TRecord> other) => this.SymmetricExceptWith((ISeamlessSet<TKey,TRecord>)other);
    public void UnionWith(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.UnionWith(this,other);
    public void UnionWith(Set<TKey,TRecord> other) => this.UnionWith((ISeamlessSet<TKey,TRecord>)other);

    public Set<TKey,TRecord> Except(Set<TKey,TRecord> other) => (Set<TKey,TRecord>)this.Except((ISeamlessSet<TKey,TRecord>)other);
    public ISeamlessSet<TKey,TRecord> Except(ISeamlessSet<TKey,TRecord> other) {
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
            if(!other.Contains(item.Key))
                result.Add(item.Key);
        }
        return result;
    }

    public Set<TKey,TRecord> Intersect(Set<TKey,TRecord> other) => (Set<TKey,TRecord>)this.Intersect((ISeamlessSet<TKey,TRecord>)other);
    public ISeamlessSet<TKey,TRecord> Intersect(ISeamlessSet<TKey,TRecord> other) {
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
            if(other.Contains(item.Key))
                result.Add(item.Key);
        }
        return result;
    }

    public Set<TKey,TRecord> SymmetricExcept(Set<TKey,TRecord> other) => (Set<TKey,TRecord>)this.SymmetricExcept((ISeamlessSet<TKey,TRecord>)other);
    public ISeamlessSet<TKey,TRecord> SymmetricExcept(ISeamlessSet<TKey,TRecord> other) {
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
            if(!other.Contains(item.Key))
                result.Add(item.Key);
        }
        foreach(var key in other) {
            if(!this.ContainsKey(key))
                result.Add(key);
        }
        return result;
    }

    public Set<TKey,TRecord> Union(Set<TKey,TRecord> other) => (Set<TKey,TRecord>)this.Union((ISeamlessSet<TKey,TRecord>)other);
    public ISeamlessSet<TKey,TRecord> Union(ISeamlessSet<TKey,TRecord> other) {
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
        var store = HashStore.Create().CreateSet<TKey,TRecord>();
        return new(store,store);
    }

    private static HashSet<TKey> CreateOwnedKeyResultSet() {
        var store = HashStore.Create().Create<TKey>();
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
}
