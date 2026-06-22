namespace Seamless.Collections.Tsavorite;

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
    void ExceptWith(ISet<TRecord> other) => TsavoriteSetOperations.ExceptWith(this,other);
    void IntersectWith(ISet<TRecord> other) => TsavoriteSetOperations.IntersectWith(this,other);
    bool IsSubsetOf(ISet<TRecord> other) => TsavoriteSetOperations.IsSubsetOf(this,other);
    bool IsSupersetOf(ISet<TRecord> other) => TsavoriteSetOperations.IsSupersetOf(this,other);
    bool IsProperSubsetOf(ISet<TRecord> other) => TsavoriteSetOperations.IsProperSubsetOf(this,other);
    bool IsProperSupersetOf(ISet<TRecord> other) => TsavoriteSetOperations.IsProperSupersetOf(this,other);
    bool Overlaps(ISet<TRecord> other) => TsavoriteSetOperations.Overlaps(this,other);
    bool SetEquals(ISet<TRecord> other) => TsavoriteSetOperations.SetEquals(this,other);
    void SymmetricExceptWith(ISet<TRecord> other) => TsavoriteSetOperations.SymmetricExceptWith(this,other);
    void UnionWith(ISet<TRecord> other) => TsavoriteSetOperations.UnionWith(this,other);
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
public interface ISet<in TKey, TRecord>:ISet<TRecord>
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
    //void ExceptWith(ISet<TKey,TRecord> other) => TsavoriteSetOperations.ExceptWith(this,other);
    //void IntersectWith(ISet<TKey,TRecord> other) => TsavoriteSetOperations.IntersectWith(this,other);
    //bool IsSubsetOf(ISet<TKey,TRecord> other) => TsavoriteSetOperations.IsSubsetOf(this,other);
    //bool IsSupersetOf(ISet<TKey,TRecord> other) => TsavoriteSetOperations.IsSupersetOf(this,other);
    //bool IsProperSubsetOf(ISet<TKey,TRecord> other) => TsavoriteSetOperations.IsProperSubsetOf(this,other);
    //bool IsProperSupersetOf(ISet<TKey,TRecord> other) => TsavoriteSetOperations.IsProperSupersetOf(this,other);
    //bool Overlaps(ISet<TKey,TRecord> other) => TsavoriteSetOperations.Overlaps(this,other);
    //bool SetEquals(ISet<TKey,TRecord> other) => TsavoriteSetOperations.SetEquals(this,other);
    //void SymmetricExceptWith(ISet<TKey,TRecord> other) => TsavoriteSetOperations.SymmetricExceptWith(this,other);
    //void UnionWith(ISet<TKey,TRecord> other) => TsavoriteSetOperations.UnionWith(this,other);
}

internal static class TsavoriteSetOperations {
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
