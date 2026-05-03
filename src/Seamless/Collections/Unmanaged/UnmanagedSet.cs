using System.Diagnostics;
using Seamless.Stores;

namespace Seamless.Collections;

public partial class UnmanagedSet<TKey, TRecord>:
    UnmanagedUnmanagedCollection<TKey,TRecord,UnmanagedSerializer<TKey>,UnmanagedSerializer<TRecord>>,
    ISeamlessSet<TKey,TRecord>
    where TRecord : unmanaged, IRecord<TKey>
    where TKey : unmanaged {
    private readonly IDisposable? ownedStore;
    internal UnmanagedSet(UnmanagedCollectionStore<TKey,TRecord> collectionStore,IDisposable? ownedStore = null) : base(collectionStore) {
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
    public bool Remove(TKey key,out TRecord value) {
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
    public void ExceptWith(UnmanagedSet<TKey,TRecord> other) => this.ExceptWith((ISeamlessSet<TKey,TRecord>)other);
    public void IntersectWith(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.IntersectWith(this,other);
    public void IntersectWith(UnmanagedSet<TKey,TRecord> other) => this.IntersectWith((ISeamlessSet<TKey,TRecord>)other);
    public bool IsSubsetOf(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.IsSubsetOf(this,other);
    public bool IsSubsetOf(UnmanagedSet<TKey,TRecord> other) => this.IsSubsetOf((ISeamlessSet<TKey,TRecord>)other);
    public bool IsSupersetOf(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.IsSupersetOf(this,other);
    public bool IsSupersetOf(UnmanagedSet<TKey,TRecord> other) => this.IsSupersetOf((ISeamlessSet<TKey,TRecord>)other);
    public bool IsProperSubsetOf(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.IsProperSubsetOf(this,other);
    public bool IsProperSubsetOf(UnmanagedSet<TKey,TRecord> other) => this.IsProperSubsetOf((ISeamlessSet<TKey,TRecord>)other);
    public bool IsProperSupersetOf(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.IsProperSupersetOf(this,other);
    public bool IsProperSupersetOf(UnmanagedSet<TKey,TRecord> other) => this.IsProperSupersetOf((ISeamlessSet<TKey,TRecord>)other);
    public bool Overlaps(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.Overlaps(this,other);
    public bool Overlaps(UnmanagedSet<TKey,TRecord> other) => this.Overlaps((ISeamlessSet<TKey,TRecord>)other);
    public bool SetEquals(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.SetEquals(this,other);
    public bool SetEquals(UnmanagedSet<TKey,TRecord> other) => this.SetEquals((ISeamlessSet<TKey,TRecord>)other);
    public void SymmetricExceptWith(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.SymmetricExceptWith(this,other);
    public void SymmetricExceptWith(UnmanagedSet<TKey,TRecord> other) => this.SymmetricExceptWith((ISeamlessSet<TKey,TRecord>)other);
    public void UnionWith(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.UnionWith(this,other);
    public void UnionWith(UnmanagedSet<TKey,TRecord> other) => this.UnionWith((ISeamlessSet<TKey,TRecord>)other);

    public UnmanagedSet<TKey,TRecord> Except(UnmanagedSet<TKey,TRecord> other) => (UnmanagedSet<TKey,TRecord>)this.Except((ISeamlessSet<TKey,TRecord>)other);
    public ISeamlessSet<TKey,TRecord> Except(ISeamlessSet<TKey,TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedResultSet();
        foreach(var item in this) {
            if(!other.ContainsKey(item.Key))
                result.Add(item);
        }
        return result;
    }
    public UnmanagedHashSet<TKey> Except(UnmanagedHashSet<TKey> other) => (UnmanagedHashSet<TKey>)this.Except((IHashSet<TKey>)other);
    public IHashSet<TKey> Except(IHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedKeyResultSet();
        foreach(var item in this) {
            if(!other.Contains(item.Key))
                result.Add(item.Key);
        }
        return result;
    }

    public UnmanagedSet<TKey,TRecord> Intersect(UnmanagedSet<TKey,TRecord> other) => (UnmanagedSet<TKey,TRecord>)this.Intersect((ISeamlessSet<TKey,TRecord>)other);
    public ISeamlessSet<TKey,TRecord> Intersect(ISeamlessSet<TKey,TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedResultSet();
        foreach(var item in this) {
            if(other.ContainsKey(item.Key))
                result.Add(item);
        }
        return result;
    }
    public UnmanagedHashSet<TKey> Intersect(UnmanagedHashSet<TKey> other) => (UnmanagedHashSet<TKey>)this.Intersect((IHashSet<TKey>)other);
    public IHashSet<TKey> Intersect(IHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedKeyResultSet();
        foreach(var item in this) {
            if(other.Contains(item.Key))
                result.Add(item.Key);
        }
        return result;
    }

    public UnmanagedSet<TKey,TRecord> SymmetricExcept(UnmanagedSet<TKey,TRecord> other) => (UnmanagedSet<TKey,TRecord>)this.SymmetricExcept((ISeamlessSet<TKey,TRecord>)other);
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
    public UnmanagedHashSet<TKey> SymmetricExcept(UnmanagedHashSet<TKey> other) => (UnmanagedHashSet<TKey>)this.SymmetricExcept((IHashSet<TKey>)other);
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

    public UnmanagedSet<TKey,TRecord> Union(UnmanagedSet<TKey,TRecord> other) => (UnmanagedSet<TKey,TRecord>)this.Union((ISeamlessSet<TKey,TRecord>)other);
    public ISeamlessSet<TKey,TRecord> Union(ISeamlessSet<TKey,TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedResultSet();
        foreach(var item in this)
            result.Add(item);
        foreach(var item in other)
            result.Add(item);
        return result;
    }
    public UnmanagedHashSet<TKey> Union(UnmanagedHashSet<TKey> other) => (UnmanagedHashSet<TKey>)this.Union((IHashSet<TKey>)other);
    public IHashSet<TKey> Union(IHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedKeyResultSet();
        foreach(var item in this)
            result.Add(item.Key);
        foreach(var key in other)
            result.Add(key);
        return result;
    }

    private static UnmanagedSet<TKey,TRecord> CreateOwnedResultSet() {
        var store = HashStore.Create().CreateUnmanagedSet<TKey,TRecord>();
        return new(store,store);
    }

    private static UnmanagedHashSet<TKey> CreateOwnedKeyResultSet() {
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

public class UnmanagedSet<TRecord>:UnmanagedList<TRecord>
    where TRecord : unmanaged {
    public UnmanagedSet(string path,long initialCapacity = 16) : base(path,initialCapacity) {
    }

    public UnmanagedSet<TRecord> Distinct() {
        this.DuplicateRemoval();
        return this;
    }
}
