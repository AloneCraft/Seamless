namespace Seamless.Collections.Tsavorite;

public interface ISetIterateStrategy<TSelf,in TRecord>
    where TSelf : struct, ISetIterateStrategy<TSelf,TRecord> {
    static abstract bool Invoke(ref TSelf self,TRecord item);
}

public class Set<TKey, TRecord>:Collection<TKey,TRecord,AutoSerializer<TKey>,AutoSerializer<TRecord>>, ISet<TKey,TRecord>, IMemoryCollection<TRecord>
    where TRecord : IRecord<TKey> {
    private readonly IDisposable? ownedStore;
    internal Set(CollectionStore<TKey,TRecord> collectionStore,IDisposable? ownedStore = null) : base(collectionStore) {
        // 構築に成功してから受け取る(失敗したときは呼び出し側が破棄する)。
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
    public void ExceptWith(ISet<TKey,TRecord> other) => TsavoriteSetOperations.ExceptWith(this,other);
    public void ExceptWith(Set<TKey,TRecord> other) => this.ExceptWith((ISet<TKey,TRecord>)other);
    public void IntersectWith(ISet<TKey,TRecord> other) => TsavoriteSetOperations.IntersectWith(this,other);
    public void IntersectWith(Set<TKey,TRecord> other) => this.IntersectWith((ISet<TKey,TRecord>)other);
    public bool IsSubsetOf(ISet<TKey,TRecord> other) => TsavoriteSetOperations.IsSubsetOf(this,other);
    public bool IsSubsetOf(Set<TKey,TRecord> other) => this.IsSubsetOf((ISet<TKey,TRecord>)other);
    public bool IsSupersetOf(ISet<TKey,TRecord> other) => TsavoriteSetOperations.IsSupersetOf(this,other);
    public bool IsSupersetOf(Set<TKey,TRecord> other) => this.IsSupersetOf((ISet<TKey,TRecord>)other);
    public bool IsProperSubsetOf(ISet<TKey,TRecord> other) => TsavoriteSetOperations.IsProperSubsetOf(this,other);
    public bool IsProperSubsetOf(Set<TKey,TRecord> other) => this.IsProperSubsetOf((ISet<TKey,TRecord>)other);
    public bool IsProperSupersetOf(ISet<TKey,TRecord> other) => TsavoriteSetOperations.IsProperSupersetOf(this,other);
    public bool IsProperSupersetOf(Set<TKey,TRecord> other) => this.IsProperSupersetOf((ISet<TKey,TRecord>)other);
    public bool Overlaps(ISet<TKey,TRecord> other) => TsavoriteSetOperations.Overlaps(this,other);
    public bool Overlaps(Set<TKey,TRecord> other) => this.Overlaps((ISet<TKey,TRecord>)other);
    public bool SetEquals(ISet<TKey,TRecord> other) => TsavoriteSetOperations.SetEquals(this,other);
    public bool SetEquals(Set<TKey,TRecord> other) => this.SetEquals((ISet<TKey,TRecord>)other);
    public void SymmetricExceptWith(ISet<TKey,TRecord> other) => TsavoriteSetOperations.SymmetricExceptWith(this,other);
    public void SymmetricExceptWith(Set<TKey,TRecord> other) => this.SymmetricExceptWith((ISet<TKey,TRecord>)other);
    public void UnionWith(ISet<TKey,TRecord> other) => TsavoriteSetOperations.UnionWith(this,other);
    public void UnionWith(Set<TKey,TRecord> other) => this.UnionWith((ISet<TKey,TRecord>)other);

    public Set<TKey,TRecord> Except(Set<TKey,TRecord> other) => this.Except((ISet<TKey,TRecord>)other);
    public Set<TKey,TRecord> Except(ISet<TKey,TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedResultSet();
        foreach(var item in this) {
            if(!other.ContainsKey(item.Key))
                result.Add(item);
        }
        return result;
    }
    public HashSet<TKey> Except(HashSet<TKey> other) => this.Except((IHashSet<TKey>)other);
    public HashSet<TKey> Except(IHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedKeyResultSet();
        foreach(var item in this) {
            if(!((ICollection<TKey>)other).Contains(item.Key))
                result.Add(item.Key);
        }
        return result;
    }

    public Set<TKey,TRecord> Intersect(Set<TKey,TRecord> other) => this.Intersect((ISet<TKey,TRecord>)other);
    public Set<TKey,TRecord> Intersect(ISet<TKey,TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedResultSet();
        foreach(var item in this) {
            if(other.ContainsKey(item.Key))
                result.Add(item);
        }
        return result;
    }
    public HashSet<TKey> Intersect(HashSet<TKey> other) => this.Intersect((IHashSet<TKey>)other);
    public HashSet<TKey> Intersect(IHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedKeyResultSet();
        foreach(var item in this) {
            if(((ICollection<TKey>)other).Contains(item.Key))
                result.Add(item.Key);
        }
        return result;
    }

    public Set<TKey,TRecord> SymmetricExcept(Set<TKey,TRecord> other) => this.SymmetricExcept((ISet<TKey,TRecord>)other);
    public Set<TKey,TRecord> SymmetricExcept(ISet<TKey,TRecord> other) {
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
    public HashSet<TKey> SymmetricExcept(HashSet<TKey> other) => this.SymmetricExcept((IHashSet<TKey>)other);
    public HashSet<TKey> SymmetricExcept(IHashSet<TKey> other) {
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

    public Set<TKey,TRecord> Union(Set<TKey,TRecord> other) => this.Union((ISet<TKey,TRecord>)other);
    public Set<TKey,TRecord> Union(ISet<TKey,TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        var result = CreateOwnedResultSet();
        foreach(var item in this)
            result.Add(item);
        foreach(var item in other)
            result.Add(item);
        return result;
    }
    public HashSet<TKey> Union(HashSet<TKey> other) => this.Union((IHashSet<TKey>)other);
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
        var store = global::Seamless.Collections.Tsavorite.Store.Create().CreateSet<TKey,TRecord>();
        try {
            return new(store,store);
        } catch {
            store.Dispose();
            throw;
        }
    }

    private static HashSet<TKey> CreateOwnedKeyResultSet() {
        var store = global::Seamless.Collections.Tsavorite.Store.Create().Create<TKey>();
        try {
            return store.CreateOwned();
        } catch {
            store.Dispose();
            throw;
        }
    }

    public override bool IsDisposed => this.disposed;
    private bool disposed;
    protected override void Dispose(bool disposing) {
        if(this.disposed)
            return;
        this.disposed=true;
        try {
            base.Dispose(disposing);
        } finally {
            if(disposing)
                this.ownedStore?.Dispose();
        }
    }
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    public struct ValueEnumerator {
        private Set<TKey,TRecord>? source;
        private Enumerator<ValueIterator,TRecord> enumerator;
        private bool initialized;

        internal ValueEnumerator(Set<TKey,TRecord> source) {
            this.source=source;
            this.enumerator=default;
            this.initialized=false;
        }

        public bool TryGetNext(out TRecord current) {
            if(!this.initialized) {
                var source=this.source!;
                source.VerifyThreadAffinity();
                this.enumerator=new(source.ReadSession,new(),source.Store.Log.TailAddress,source);
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
    public IEnumerator<TRecord> GetEnumerator() {
        this.VerifyThreadAffinity();
        return new Enumerator<ValueIterator,TRecord>(this.ReadSession,new(),this.Store.Log.TailAddress,this);
    }
    public void Iterate<TStrategy>(ref TStrategy strategy)
        where TStrategy : struct, ISetIterateStrategy<TStrategy,TRecord> {
        this.VerifyThreadAffinity();
        var iterator = new IterateLookupAdapter<TStrategy>(strategy);
        var cursor = 0L;
        this.ReadSession.IterateLookup(ref iterator,ref cursor,resetCursor: false);
        this.ReadSession.BasicContext.CompletePending(wait: true);
        strategy=iterator.Strategy;
    }
    IEnumerator<TRecord> IEnumerable<TRecord>.GetEnumerator() => this.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    IEnumerator<TRecord> IMemoryCollection<TRecord>.GetEnumerator() {
        this.VerifyThreadAffinity();
        return new Enumerator<MemoryValueIterator,TRecord>(this.ReadSession,new(),this.Store.Log.TailAddress,this);
    }

    private sealed class IterateLookupAdapter<TStrategy>(TStrategy strategy):IScanIteratorFunctions
        where TStrategy : struct, ISetIterateStrategy<TStrategy,TRecord> {
        public TStrategy Strategy = strategy;

        public bool Reader<TSourceLogRecord>(
            in TSourceLogRecord logRecord,
            RecordMetadata recordMetadata,
            long numberOfRecords,
            out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            if(logRecord.Info.Tombstone) {
                cursorRecordResult=CursorRecordResult.Skip;
                return true;
            }
            var item = AutoSerializer<TRecord>.Deserialize(logRecord.ValueSpan);
            if(TStrategy.Invoke(ref this.Strategy,item)) {
                cursorRecordResult=CursorRecordResult.Accept;
                return true;
            }
            cursorRecordResult=CursorRecordResult.Skip|CursorRecordResult.EndBatch;
            return true;
        }

        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnStop(bool completed,long numberOfRecords) { }
        public void OnException(Exception exception,long numberOfRecords) { }
    }
}




public class Set<TRecord>:HashSet<TRecord> {
    internal Set(CollectionStore<TRecord,EqualityEmpty> collectionStore,IDisposable? ownedStore = null) : base(collectionStore,ownedStore) {
    }
}
