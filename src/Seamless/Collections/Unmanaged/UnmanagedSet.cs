using System.Diagnostics;
using Seamless.Stores;

namespace Seamless.Collections;

public partial class UnmanagedSet<TRecord, TKey>:
    UnmanagedUnmanagedCollection<TKey,TRecord,UnmanagedSerializer<TKey>,UnmanagedSerializer<TRecord>>,
    ISeamlessSet<TRecord,TKey>
    where TRecord : unmanaged, IRecord<TKey>, IEquatable<TRecord>
    where TKey : unmanaged, IEquatable<TKey> {
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
    public void ExceptWith(ISeamlessSet<TRecord,TKey> other) => SeamlessSetOperations.ExceptWith(this,other);
    public void ExceptWith(UnmanagedSet<TRecord,TKey> other) => this.ExceptWith((ISeamlessSet<TRecord,TKey>)other);
    public void IntersectWith(ISeamlessSet<TRecord,TKey> other) => SeamlessSetOperations.IntersectWith(this,other);
    public void IntersectWith(UnmanagedSet<TRecord,TKey> other) => this.IntersectWith((ISeamlessSet<TRecord,TKey>)other);
    public bool IsSubsetOf(ISeamlessSet<TRecord,TKey> other) => SeamlessSetOperations.IsSubsetOf(this,other);
    public bool IsSubsetOf(UnmanagedSet<TRecord,TKey> other) => this.IsSubsetOf((ISeamlessSet<TRecord,TKey>)other);
    public bool IsSupersetOf(ISeamlessSet<TRecord,TKey> other) => SeamlessSetOperations.IsSupersetOf(this,other);
    public bool IsSupersetOf(UnmanagedSet<TRecord,TKey> other) => this.IsSupersetOf((ISeamlessSet<TRecord,TKey>)other);
    public bool IsProperSubsetOf(ISeamlessSet<TRecord,TKey> other) => SeamlessSetOperations.IsProperSubsetOf(this,other);
    public bool IsProperSubsetOf(UnmanagedSet<TRecord,TKey> other) => this.IsProperSubsetOf((ISeamlessSet<TRecord,TKey>)other);
    public bool IsProperSupersetOf(ISeamlessSet<TRecord,TKey> other) => SeamlessSetOperations.IsProperSupersetOf(this,other);
    public bool IsProperSupersetOf(UnmanagedSet<TRecord,TKey> other) => this.IsProperSupersetOf((ISeamlessSet<TRecord,TKey>)other);
    public bool Overlaps(ISeamlessSet<TRecord,TKey> other) => SeamlessSetOperations.Overlaps(this,other);
    public bool Overlaps(UnmanagedSet<TRecord,TKey> other) => this.Overlaps((ISeamlessSet<TRecord,TKey>)other);
    public bool SetEquals(ISeamlessSet<TRecord,TKey> other) => SeamlessSetOperations.SetEquals(this,other);
    public bool SetEquals(UnmanagedSet<TRecord,TKey> other) => this.SetEquals((ISeamlessSet<TRecord,TKey>)other);
    public void SymmetricExceptWith(ISeamlessSet<TRecord,TKey> other) => SeamlessSetOperations.SymmetricExceptWith(this,other);
    public void SymmetricExceptWith(UnmanagedSet<TRecord,TKey> other) => this.SymmetricExceptWith((ISeamlessSet<TRecord,TKey>)other);
    public void UnionWith(ISeamlessSet<TRecord,TKey> other) => SeamlessSetOperations.UnionWith(this,other);
    public void UnionWith(UnmanagedSet<TRecord,TKey> other) => this.UnionWith((ISeamlessSet<TRecord,TKey>)other);

    public UnmanagedSet<TRecord,TKey> Except(UnmanagedSet<TRecord,TKey> other) => (UnmanagedSet<TRecord,TKey>)this.Except((ISeamlessSet<TRecord,TKey>)other);
    public ISeamlessSet<TRecord,TKey> Except(ISeamlessSet<TRecord,TKey> other) {
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

    public UnmanagedSet<TRecord,TKey> Intersect(UnmanagedSet<TRecord,TKey> other) => (UnmanagedSet<TRecord,TKey>)this.Intersect((ISeamlessSet<TRecord,TKey>)other);
    public ISeamlessSet<TRecord,TKey> Intersect(ISeamlessSet<TRecord,TKey> other) {
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

    public UnmanagedSet<TRecord,TKey> SymmetricExcept(UnmanagedSet<TRecord,TKey> other) => (UnmanagedSet<TRecord,TKey>)this.SymmetricExcept((ISeamlessSet<TRecord,TKey>)other);
    public ISeamlessSet<TRecord,TKey> SymmetricExcept(ISeamlessSet<TRecord,TKey> other) {
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

    public UnmanagedSet<TRecord,TKey> Union(UnmanagedSet<TRecord,TKey> other) => (UnmanagedSet<TRecord,TKey>)this.Union((ISeamlessSet<TRecord,TKey>)other);
    public ISeamlessSet<TRecord,TKey> Union(ISeamlessSet<TRecord,TKey> other) {
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

    private static UnmanagedSet<TRecord,TKey> CreateOwnedResultSet() {
        var store = HashStore.Create().CreateUnmanagedSet<TRecord,TKey>();
        return new(store,store);
    }

    private static UnmanagedHashSet<TKey> CreateOwnedKeyResultSet() {
        var store = HashStore.Create().CreateUnmanagedHashSet<TKey>();
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
