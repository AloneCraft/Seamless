namespace Seamless.Collections;

public interface ISet<TRecord>:IMemoryCollection<TRecord>, IReadOnlyCollection<TRecord>, IDisposable {
    public interface IForEachStrategy<TSelf, TItem>
        where TSelf : struct, IForEachStrategy<TSelf,TItem> {
        static abstract bool Reader(ref TSelf self,in TItem item,long numberOfRecords);
    }
    private struct ForEachIterator<TStrategy>(TStrategy strategy)
        where TStrategy : struct, IForEachStrategy<TStrategy,TRecord> {
        public TStrategy Strategy = strategy;
        public long NumberOfRecords;
        public bool Reader(in TRecord item) {
            this.NumberOfRecords++;
            return TStrategy.Reader(ref this.Strategy,in item,this.NumberOfRecords);
        }
    }
    bool IsDisposed { get; }
    long EntryCount { get; }
    long GetLongCount();
    void ExceptWith(ISet<TRecord> other) => SeamlessSetOperations.ExceptWith(this,other);
    void IntersectWith(ISet<TRecord> other) => SeamlessSetOperations.IntersectWith(this,other);
    bool IsSubsetOf(ISet<TRecord> other) => SeamlessSetOperations.IsSubsetOf(this,other);
    bool IsSupersetOf(ISet<TRecord> other) => SeamlessSetOperations.IsSupersetOf(this,other);
    bool IsProperSubsetOf(ISet<TRecord> other) => SeamlessSetOperations.IsProperSubsetOf(this,other);
    bool IsProperSupersetOf(ISet<TRecord> other) => SeamlessSetOperations.IsProperSupersetOf(this,other);
    bool Overlaps(ISet<TRecord> other) => SeamlessSetOperations.Overlaps(this,other);
    bool SetEquals(ISet<TRecord> other) => SeamlessSetOperations.SetEquals(this,other);
    void SymmetricExceptWith(ISet<TRecord> other) => SeamlessSetOperations.SymmetricExceptWith(this,other);
    void UnionWith(ISet<TRecord> other) => SeamlessSetOperations.UnionWith(this,other);
    public void ForEach<TStrategy>(ref TStrategy strategy)
        where TStrategy : struct, IForEachStrategy<TStrategy,TRecord> {
        var iterator = new ForEachIterator<TStrategy>(strategy);
        foreach(var item in this) {
            if(!iterator.Reader(in item)) {
                break;
            }
        }
        strategy=iterator.Strategy;
    }
    public void ForEach<TStrategy>(ISet<TRecord> collection,ref TStrategy strategy)
        where TStrategy : struct, IForEachStrategy<TStrategy,TRecord> {
        ArgumentNullException.ThrowIfNull(collection);
        collection.ForEach(ref strategy);
    }
}
public interface ISeamlessSet<TKey, TRecord>:ISet<TRecord>
    where TRecord : IRecord<TKey> {
    //bool IsDisposed { get; }
    //long EntryCount { get; }
    //void Refresh() {
    //}
    new bool Add(TRecord item);
    bool ContainsKey(TKey key);
    bool TryGetValue(TKey key,[MaybeNullWhen(false)] out TRecord value);
    bool Remove(TKey key);
    //bool Remove(TKey key,[MaybeNullWhen(false)] out TRecord value);
    //void ExceptWith(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.ExceptWith(this,other);
    //void IntersectWith(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.IntersectWith(this,other);
    //bool IsSubsetOf(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.IsSubsetOf(this,other);
    //bool IsSupersetOf(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.IsSupersetOf(this,other);
    //bool IsProperSubsetOf(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.IsProperSubsetOf(this,other);
    //bool IsProperSupersetOf(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.IsProperSupersetOf(this,other);
    //bool Overlaps(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.Overlaps(this,other);
    //bool SetEquals(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.SetEquals(this,other);
    //void SymmetricExceptWith(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.SymmetricExceptWith(this,other);
    //void UnionWith(ISeamlessSet<TKey,TRecord> other) => SeamlessSetOperations.UnionWith(this,other);
}

internal static class SeamlessSetOperations {
    public static void ExceptWith<TRecord>(ISet<TRecord> target,ISet<TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in (ICollection<TRecord>)other)
            target.Remove(item);
    }
    public static void IntersectWith<TRecord>(ISet<TRecord> target,ISet<TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        var toRemove = new global::System.Collections.Generic.List<TRecord>();
        foreach(var item in (ICollection<TRecord>)target) {
            if(!other.Contains(item))
                toRemove.Add(item);
        }
        foreach(var item in toRemove)
            target.Remove(item);
    }
    public static bool IsSubsetOf<TRecord>(ISet<TRecord> target,ISet<TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in (ICollection<TRecord>)target) {
            if(!other.Contains(item))
                return false;
        }
        return true;
    }
    public static bool IsSupersetOf<TRecord>(ISet<TRecord> target,ISet<TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in (ICollection<TRecord>)other) {
            if(!target.Contains(item))
                return false;
        }
        return true;
    }
    public static bool IsProperSubsetOf<TRecord>(ISet<TRecord> target,ISet<TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        if(target.GetLongCount()>=other.GetLongCount())
            return false;
        return IsSubsetOf(target,other);
    }
    public static bool IsProperSupersetOf<TRecord>(ISet<TRecord> target,ISet<TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        if(target.GetLongCount()<=other.GetLongCount())
            return false;
        return IsSupersetOf(target,other);
    }
    public static bool Overlaps<TRecord>(ISet<TRecord> target,ISet<TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in (ICollection<TRecord>)other) {
            if(target.Contains(item))
                return true;
        }
        return false;
    }
    public static bool SetEquals<TRecord>(ISet<TRecord> target,ISet<TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        if(target.GetLongCount()!=other.GetLongCount())
            return false;
        return IsSubsetOf(target,other);
    }
    public static void SymmetricExceptWith<TRecord>(ISet<TRecord> target,ISet<TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        var toAdd = new global::System.Collections.Generic.List<TRecord>();
        var toRemove = new global::System.Collections.Generic.List<TRecord>();
        foreach(var item in (ICollection<TRecord>)other) {
            if(target.Contains(item))
                toRemove.Add(item);
            else
                toAdd.Add(item);
        }
        foreach(var key in toRemove)
            target.Remove(key);
        foreach(var item in toAdd)
            target.Add(item);
    }
    public static void UnionWith<TRecord>(ISet<TRecord> target,ISet<TRecord> other) {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in (ICollection<TRecord>)other)
            target.Add(item);
    }
}
