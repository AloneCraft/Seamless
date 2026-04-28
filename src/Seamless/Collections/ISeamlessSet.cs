using System.Diagnostics.CodeAnalysis;

namespace Seamless.Collections;

public interface ISeamlessSet<TRecord, TKey>:IMemoryCollection<TRecord>, IReadOnlyCollection<TRecord>, IDisposable
    where TRecord : IRecord<TKey>, IEquatable<TRecord>
    where TKey : IEquatable<TKey> {
    bool IsDisposed { get; }
    long EntryCount { get; }
    void Refresh() {
    }
    new bool Add(TRecord item);
    bool ContainsKey(TKey key);
    bool TryGetValue(TKey key,[MaybeNullWhen(false)] out TRecord value);
    bool Remove(TKey key);
    bool Remove(TKey key,[MaybeNullWhen(false)] out TRecord value);
    void ExceptWith(ISeamlessSet<TRecord,TKey> other) => SeamlessSetOperations.ExceptWith(this,other);
    void IntersectWith(ISeamlessSet<TRecord,TKey> other) => SeamlessSetOperations.IntersectWith(this,other);
    bool IsSubsetOf(ISeamlessSet<TRecord,TKey> other) => SeamlessSetOperations.IsSubsetOf(this,other);
    bool IsSupersetOf(ISeamlessSet<TRecord,TKey> other) => SeamlessSetOperations.IsSupersetOf(this,other);
    bool IsProperSubsetOf(ISeamlessSet<TRecord,TKey> other) => SeamlessSetOperations.IsProperSubsetOf(this,other);
    bool IsProperSupersetOf(ISeamlessSet<TRecord,TKey> other) => SeamlessSetOperations.IsProperSupersetOf(this,other);
    bool Overlaps(ISeamlessSet<TRecord,TKey> other) => SeamlessSetOperations.Overlaps(this,other);
    bool SetEquals(ISeamlessSet<TRecord,TKey> other) => SeamlessSetOperations.SetEquals(this,other);
    void SymmetricExceptWith(ISeamlessSet<TRecord,TKey> other) => SeamlessSetOperations.SymmetricExceptWith(this,other);
    void UnionWith(ISeamlessSet<TRecord,TKey> other) => SeamlessSetOperations.UnionWith(this,other);
}

internal static class SeamlessSetOperations {
    public static void ExceptWith<TRecord, TKey>(ISeamlessSet<TRecord,TKey> target,ISeamlessSet<TRecord,TKey> other)
        where TRecord : IRecord<TKey>, IEquatable<TRecord>
        where TKey : IEquatable<TKey> {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in (ICollection<TRecord>)other)
            target.Remove(item.Key);
    }
    public static void IntersectWith<TRecord, TKey>(ISeamlessSet<TRecord,TKey> target,ISeamlessSet<TRecord,TKey> other)
        where TRecord : IRecord<TKey>, IEquatable<TRecord>
        where TKey : IEquatable<TKey> {
        ArgumentNullException.ThrowIfNull(other);
        var toRemove = new global::System.Collections.Generic.List<TKey>();
        foreach(var item in (ICollection<TRecord>)target) {
            if(!other.ContainsKey(item.Key))
                toRemove.Add(item.Key);
        }
        foreach(var key in toRemove)
            target.Remove(key);
    }
    public static bool IsSubsetOf<TRecord, TKey>(ISeamlessSet<TRecord,TKey> target,ISeamlessSet<TRecord,TKey> other)
        where TRecord : IRecord<TKey>, IEquatable<TRecord>
        where TKey : IEquatable<TKey> {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in (ICollection<TRecord>)target) {
            if(!other.ContainsKey(item.Key))
                return false;
        }
        return true;
    }
    public static bool IsSupersetOf<TRecord, TKey>(ISeamlessSet<TRecord,TKey> target,ISeamlessSet<TRecord,TKey> other)
        where TRecord : IRecord<TKey>, IEquatable<TRecord>
        where TKey : IEquatable<TKey> {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in (ICollection<TRecord>)other) {
            if(!target.ContainsKey(item.Key))
                return false;
        }
        return true;
    }
    public static bool IsProperSubsetOf<TRecord, TKey>(ISeamlessSet<TRecord,TKey> target,ISeamlessSet<TRecord,TKey> other)
        where TRecord : IRecord<TKey>, IEquatable<TRecord>
        where TKey : IEquatable<TKey> {
        ArgumentNullException.ThrowIfNull(other);
        if(((IReadOnlyCollection<TRecord>)target).Count>=((IReadOnlyCollection<TRecord>)other).Count)
            return false;
        return IsSubsetOf(target,other);
    }
    public static bool IsProperSupersetOf<TRecord, TKey>(ISeamlessSet<TRecord,TKey> target,ISeamlessSet<TRecord,TKey> other)
        where TRecord : IRecord<TKey>, IEquatable<TRecord>
        where TKey : IEquatable<TKey> {
        ArgumentNullException.ThrowIfNull(other);
        if(((IReadOnlyCollection<TRecord>)target).Count<=((IReadOnlyCollection<TRecord>)other).Count)
            return false;
        return IsSupersetOf(target,other);
    }
    public static bool Overlaps<TRecord, TKey>(ISeamlessSet<TRecord,TKey> target,ISeamlessSet<TRecord,TKey> other)
        where TRecord : IRecord<TKey>, IEquatable<TRecord>
        where TKey : IEquatable<TKey> {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in (ICollection<TRecord>)other) {
            if(target.ContainsKey(item.Key))
                return true;
        }
        return false;
    }
    public static bool SetEquals<TRecord, TKey>(ISeamlessSet<TRecord,TKey> target,ISeamlessSet<TRecord,TKey> other)
        where TRecord : IRecord<TKey>, IEquatable<TRecord>
        where TKey : IEquatable<TKey> {
        ArgumentNullException.ThrowIfNull(other);
        if(((IReadOnlyCollection<TRecord>)target).Count!=((IReadOnlyCollection<TRecord>)other).Count)
            return false;
        return IsSubsetOf(target,other);
    }
    public static void SymmetricExceptWith<TRecord, TKey>(ISeamlessSet<TRecord,TKey> target,ISeamlessSet<TRecord,TKey> other)
        where TRecord : IRecord<TKey>, IEquatable<TRecord>
        where TKey : IEquatable<TKey> {
        ArgumentNullException.ThrowIfNull(other);
        var toAdd = new global::System.Collections.Generic.List<TRecord>();
        var toRemove = new global::System.Collections.Generic.List<TKey>();
        foreach(var item in (ICollection<TRecord>)other) {
            if(target.ContainsKey(item.Key))
                toRemove.Add(item.Key);
            else
                toAdd.Add(item);
        }
        foreach(var key in toRemove)
            target.Remove(key);
        foreach(var item in toAdd)
            target.Add(item);
    }
    public static void UnionWith<TRecord, TKey>(ISeamlessSet<TRecord,TKey> target,ISeamlessSet<TRecord,TKey> other)
        where TRecord : IRecord<TKey>, IEquatable<TRecord>
        where TKey : IEquatable<TKey> {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in (ICollection<TRecord>)other)
            target.Add(item);
    }
}
