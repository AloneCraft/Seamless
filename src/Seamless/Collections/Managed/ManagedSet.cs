using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Seamless.Stores;

namespace Seamless.Collections;

public class ManagedSet<TRecord>:ManagedList<TRecord>, ISet<TRecord>
    where TRecord : IEquatable<TRecord> {

    public ManagedSet(string path,long initialCapacity = 16,long initialDataCapacity = 4096) :
        base(path,initialCapacity,initialDataCapacity) {
    }

    public ManagedSet<TRecord> Distinct() {
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
    public IEnumerator<TRecord> GetEnumerator() {
        throw new NotImplementedException();
    }
    public long EntryCount => this.GetLongCount();
}
public partial class ManagedSet<TRecord, TKey>:
    ManagedManagedCollection<TKey,TRecord,ManagedSerializer<TKey>,ManagedSerializer<TRecord>>,
    ISeamlessSet<TRecord,TKey>
    where TRecord : IRecord<TKey>, IEquatable<TRecord>
    where TKey : IEquatable<TKey> {
    private readonly IDisposable? ownedStore;
    internal ManagedSet(ManagedCollectionStore<TKey,TRecord> collectionStore,IDisposable? ownedStore = null) : base(collectionStore) {
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
    public void ExceptWith(ISeamlessSet<TRecord,TKey> other) => SeamlessSetOperations.ExceptWith(this,other);
    public void ExceptWith(ManagedSet<TRecord,TKey> other) => this.ExceptWith((ISeamlessSet<TRecord,TKey>)other);
    public void IntersectWith(ISeamlessSet<TRecord,TKey> other) => SeamlessSetOperations.IntersectWith(this,other);
    public void IntersectWith(ManagedSet<TRecord,TKey> other) => this.IntersectWith((ISeamlessSet<TRecord,TKey>)other);
    public bool IsSubsetOf(ISeamlessSet<TRecord,TKey> other) => SeamlessSetOperations.IsSubsetOf(this,other);
    public bool IsSubsetOf(ManagedSet<TRecord,TKey> other) => this.IsSubsetOf((ISeamlessSet<TRecord,TKey>)other);
    public bool IsSupersetOf(ISeamlessSet<TRecord,TKey> other) => SeamlessSetOperations.IsSupersetOf(this,other);
    public bool IsSupersetOf(ManagedSet<TRecord,TKey> other) => this.IsSupersetOf((ISeamlessSet<TRecord,TKey>)other);
    public bool IsProperSubsetOf(ISeamlessSet<TRecord,TKey> other) => SeamlessSetOperations.IsProperSubsetOf(this,other);
    public bool IsProperSubsetOf(ManagedSet<TRecord,TKey> other) => this.IsProperSubsetOf((ISeamlessSet<TRecord,TKey>)other);
    public bool IsProperSupersetOf(ISeamlessSet<TRecord,TKey> other) => SeamlessSetOperations.IsProperSupersetOf(this,other);
    public bool IsProperSupersetOf(ManagedSet<TRecord,TKey> other) => this.IsProperSupersetOf((ISeamlessSet<TRecord,TKey>)other);
    public bool Overlaps(ISeamlessSet<TRecord,TKey> other) => SeamlessSetOperations.Overlaps(this,other);
    public bool Overlaps(ManagedSet<TRecord,TKey> other) => this.Overlaps((ISeamlessSet<TRecord,TKey>)other);
    public bool SetEquals(ISeamlessSet<TRecord,TKey> other) => SeamlessSetOperations.SetEquals(this,other);
    public bool SetEquals(ManagedSet<TRecord,TKey> other) => this.SetEquals((ISeamlessSet<TRecord,TKey>)other);
    public void SymmetricExceptWith(ISeamlessSet<TRecord,TKey> other) => SeamlessSetOperations.SymmetricExceptWith(this,other);
    public void SymmetricExceptWith(ManagedSet<TRecord,TKey> other) => this.SymmetricExceptWith((ISeamlessSet<TRecord,TKey>)other);
    public void UnionWith(ISeamlessSet<TRecord,TKey> other) => SeamlessSetOperations.UnionWith(this,other);
    public void UnionWith(ManagedSet<TRecord,TKey> other) => this.UnionWith((ISeamlessSet<TRecord,TKey>)other);

    public ManagedSet<TRecord,TKey> Except(ManagedSet<TRecord,TKey> other) => (ManagedSet<TRecord,TKey>)this.Except((ISeamlessSet<TRecord,TKey>)other);
    public ISeamlessSet<TRecord,TKey> Except(ISeamlessSet<TRecord,TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedResultSet();
        foreach(var item in this) {
            if(!other.ContainsKey(item.Key))
                result.Add(item);
        }
        return result;
    }
    public ManagedHashSet<TKey> Except(ManagedHashSet<TKey> other) => (ManagedHashSet<TKey>)this.Except((IHashSet<TKey>)other);
    public IHashSet<TKey> Except(IHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedKeyResultSet();
        foreach(var item in this) {
            if(!other.Contains(item.Key))
                result.Add(item.Key);
        }
        return result;
    }

    public ManagedSet<TRecord,TKey> Intersect(ManagedSet<TRecord,TKey> other) => (ManagedSet<TRecord,TKey>)this.Intersect((ISeamlessSet<TRecord,TKey>)other);
    public ISeamlessSet<TRecord,TKey> Intersect(ISeamlessSet<TRecord,TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedResultSet();
        foreach(var item in this) {
            if(other.ContainsKey(item.Key))
                result.Add(item);
        }
        return result;
    }
    public ManagedHashSet<TKey> Intersect(ManagedHashSet<TKey> other) => (ManagedHashSet<TKey>)this.Intersect((IHashSet<TKey>)other);
    public IHashSet<TKey> Intersect(IHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedKeyResultSet();
        foreach(var item in this) {
            if(other.Contains(item.Key))
                result.Add(item.Key);
        }
        return result;
    }

    public ManagedSet<TRecord,TKey> SymmetricExcept(ManagedSet<TRecord,TKey> other) => (ManagedSet<TRecord,TKey>)this.SymmetricExcept((ISeamlessSet<TRecord,TKey>)other);
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
    public ManagedHashSet<TKey> SymmetricExcept(ManagedHashSet<TKey> other) => (ManagedHashSet<TKey>)this.SymmetricExcept((IHashSet<TKey>)other);
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

    public ManagedSet<TRecord,TKey> Union(ManagedSet<TRecord,TKey> other) => (ManagedSet<TRecord,TKey>)this.Union((ISeamlessSet<TRecord,TKey>)other);
    public ISeamlessSet<TRecord,TKey> Union(ISeamlessSet<TRecord,TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedResultSet();
        foreach(var item in this)
            result.Add(item);
        foreach(var item in other)
            result.Add(item);
        return result;
    }
    public ManagedHashSet<TKey> Union(ManagedHashSet<TKey> other) => (ManagedHashSet<TKey>)this.Union((IHashSet<TKey>)other);
    public IHashSet<TKey> Union(IHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedKeyResultSet();
        foreach(var item in this)
            result.Add(item.Key);
        foreach(var key in other)
            result.Add(key);
        return result;
    }

    private static ManagedSet<TRecord,TKey> CreateOwnedResultSet() {
        var store = HashStore.Create().CreateManagedSet<TRecord,TKey>();
        return new(store,store);
    }

    private static ManagedHashSet<TKey> CreateOwnedKeyResultSet() {
        var store = HashStore.Create().CreateManagedHashSet<TKey>();
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

