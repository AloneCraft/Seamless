using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Seamless.Stores;

namespace Seamless.Collections;

public class ManagedSet<TRecord>:ManagedList<TRecord>, ISet<TRecord> {

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
    public long EntryCount => this.GetLongCount();
    public IEnumerator<TRecord> GetEnumerator() {
        throw new NotImplementedException();
    }
    IEnumerator<TRecord> IEnumerable<TRecord>.GetEnumerator() => this.GetEnumerator();
}
public partial class ManagedSet<TKey, TRecord>:
    ManagedManagedCollection<TKey,TRecord,ManagedSerializer<TKey>,ManagedSerializer<TRecord>>,
    ISeamlessSet<TKey,TRecord>
    where TRecord : IRecord<TKey> {
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
    public void ExceptWith(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.ExceptWith(this,other);
    public void ExceptWith(ManagedSet<TKey,TRecord> other) => this.ExceptWith((ISeamlessSet<TKey,TRecord>)other);
    public void IntersectWith(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.IntersectWith(this,other);
    public void IntersectWith(ManagedSet<TKey,TRecord> other) => this.IntersectWith((ISeamlessSet<TKey,TRecord>)other);
    public bool IsSubsetOf(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.IsSubsetOf(this,other);
    public bool IsSubsetOf(ManagedSet<TKey,TRecord> other) => this.IsSubsetOf((ISeamlessSet<TKey,TRecord>)other);
    public bool IsSupersetOf(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.IsSupersetOf(this,other);
    public bool IsSupersetOf(ManagedSet<TKey,TRecord> other) => this.IsSupersetOf((ISeamlessSet<TKey,TRecord>)other);
    public bool IsProperSubsetOf(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.IsProperSubsetOf(this,other);
    public bool IsProperSubsetOf(ManagedSet<TKey,TRecord> other) => this.IsProperSubsetOf((ISeamlessSet<TKey,TRecord>)other);
    public bool IsProperSupersetOf(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.IsProperSupersetOf(this,other);
    public bool IsProperSupersetOf(ManagedSet<TKey,TRecord> other) => this.IsProperSupersetOf((ISeamlessSet<TKey,TRecord>)other);
    public bool Overlaps(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.Overlaps(this,other);
    public bool Overlaps(ManagedSet<TKey,TRecord> other) => this.Overlaps((ISeamlessSet<TKey,TRecord>)other);
    public bool SetEquals(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.SetEquals(this,other);
    public bool SetEquals(ManagedSet<TKey,TRecord> other) => this.SetEquals((ISeamlessSet<TKey,TRecord>)other);
    public void SymmetricExceptWith(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.SymmetricExceptWith(this,other);
    public void SymmetricExceptWith(ManagedSet<TKey,TRecord> other) => this.SymmetricExceptWith((ISeamlessSet<TKey,TRecord>)other);
    public void UnionWith(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.UnionWith(this,other);
    public void UnionWith(ManagedSet<TKey,TRecord> other) => this.UnionWith((ISeamlessSet<TKey,TRecord>)other);

    public ManagedSet<TKey,TRecord> Except(ManagedSet<TKey,TRecord> other) => (ManagedSet<TKey,TRecord>)this.Except((ISeamlessSet<TKey,TRecord>)other);
    public ISeamlessSet<TKey,TRecord> Except(ISeamlessSet<TKey,TRecord> other) {
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

    public ManagedSet<TKey,TRecord> Intersect(ManagedSet<TKey,TRecord> other) => (ManagedSet<TKey,TRecord>)this.Intersect((ISeamlessSet<TKey,TRecord>)other);
    public ISeamlessSet<TKey,TRecord> Intersect(ISeamlessSet<TKey,TRecord> other) {
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

    public ManagedSet<TKey,TRecord> SymmetricExcept(ManagedSet<TKey,TRecord> other) => (ManagedSet<TKey,TRecord>)this.SymmetricExcept((ISeamlessSet<TKey,TRecord>)other);
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

    public ManagedSet<TKey,TRecord> Union(ManagedSet<TKey,TRecord> other) => (ManagedSet<TKey,TRecord>)this.Union((ISeamlessSet<TKey,TRecord>)other);
    public ISeamlessSet<TKey,TRecord> Union(ISeamlessSet<TKey,TRecord> other) {
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

    private static ManagedSet<TKey,TRecord> CreateOwnedResultSet() {
        var store = HashStore.Create().CreateSet<TKey,TRecord>();
        return new(store,store);
    }

    private static ManagedHashSet<TKey> CreateOwnedKeyResultSet() {
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

