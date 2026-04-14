namespace Seamless.Collections;

public interface ISet<TKey>:ICollection<TKey> where TKey:IEquatable<TKey>{
    new bool Add(TKey item);
    void UnionWith(ManagedHashSet<TKey> other);
    void IntersectWith(ManagedHashSet<TKey> other);
    void ExceptWith(ManagedHashSet<TKey> other);
    void SymmetricExceptWith(ManagedHashSet<TKey> other);
    bool IsSubsetOf(ManagedHashSet<TKey> other);
    bool IsSupersetOf(ManagedHashSet<TKey> other);
    bool IsProperSupersetOf(ManagedHashSet<TKey> other);
    bool IsProperSubsetOf(ManagedHashSet<TKey> other);
    bool Overlaps(ManagedHashSet<TKey> other);
    bool SetEquals(ManagedHashSet<TKey> other);
}
