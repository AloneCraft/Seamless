namespace Seamless.Collections;

public interface IUnorderedEnumerable<out T>:IEnumerable<T> {
}

public interface ISet<T>:IReadOnlySet<T>, IUnorderedEnumerable<T> {
    long GetLongCount() => this.Count;

    bool IReadOnlySet<T>.IsSubsetOf(IEnumerable<T> other) {
        ArgumentNullException.ThrowIfNull(other);
        return SetRelationHelpers.IsSubsetOf(this,other);
    }

    bool IReadOnlySet<T>.IsSupersetOf(IEnumerable<T> other) {
        ArgumentNullException.ThrowIfNull(other);
        return SetRelationHelpers.IsSupersetOf(this,other);
    }

    bool IReadOnlySet<T>.IsProperSubsetOf(IEnumerable<T> other) {
        ArgumentNullException.ThrowIfNull(other);
        return SetRelationHelpers.IsProperSubsetOf(this,other);
    }

    bool IReadOnlySet<T>.IsProperSupersetOf(IEnumerable<T> other) {
        ArgumentNullException.ThrowIfNull(other);
        return SetRelationHelpers.IsProperSupersetOf(this,other);
    }

    bool IReadOnlySet<T>.Overlaps(IEnumerable<T> other) {
        ArgumentNullException.ThrowIfNull(other);
        return SetRelationHelpers.Overlaps(this,other);
    }

    bool IReadOnlySet<T>.SetEquals(IEnumerable<T> other) {
        ArgumentNullException.ThrowIfNull(other);
        return SetRelationHelpers.SetEquals(this,other);
    }
}

public interface ISet<in TKey, TRecord>:ISet<TRecord>
    where TRecord : IRecord<TKey> {
    bool ContainsKey(TKey key);
    bool TryGetValue(TKey key,[MaybeNullWhen(false)] out TRecord value);
}

internal static class SetRelationHelpers {
    public static bool IsSubsetOf<T>(ISet<T> target,IEnumerable<T> other) {
        var otherSet = Materialize(other);
        if(target.GetLongCount()>otherSet.Count)
            return false;
        foreach(var item in target) {
            if(!otherSet.Contains(item))
                return false;
        }
        return true;
    }

    public static bool IsSupersetOf<T>(ISet<T> target,IEnumerable<T> other) {
        foreach(var item in other) {
            if(!target.Contains(item))
                return false;
        }
        return true;
    }

    public static bool IsProperSubsetOf<T>(ISet<T> target,IEnumerable<T> other) {
        var otherSet = Materialize(other);
        return target.GetLongCount()<otherSet.Count&&IsSubsetOf(target,otherSet);
    }

    public static bool IsProperSupersetOf<T>(ISet<T> target,IEnumerable<T> other) {
        var otherSet = Materialize(other);
        return target.GetLongCount()>otherSet.Count&&IsSupersetOf(target,otherSet);
    }

    public static bool Overlaps<T>(ISet<T> target,IEnumerable<T> other) {
        foreach(var item in other) {
            if(target.Contains(item))
                return true;
        }
        return false;
    }

    public static bool SetEquals<T>(ISet<T> target,IEnumerable<T> other) {
        var otherSet = Materialize(other);
        return target.GetLongCount()==otherSet.Count&&IsSubsetOf(target,otherSet);
    }

    private static IReadOnlySet<T> Materialize<T>(IEnumerable<T> source) =>
        source as IReadOnlySet<T>??new global::System.Collections.Generic.HashSet<T>(source);
}
