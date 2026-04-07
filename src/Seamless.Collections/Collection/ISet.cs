namespace Seamless.Collections;
public interface ISet<T> : ICollection<T>where T: IEquatable<T> {
    new bool Add(T item);
    void UnionWith(ManagedHashSet<T> other);
    void IntersectWith(ManagedHashSet<T> other);
    void ExceptWith(ManagedHashSet<T> other);
    void SymmetricExceptWith(ManagedHashSet<T> other);
    bool IsSubsetOf(ManagedHashSet<T> other);
    bool IsSupersetOf(ManagedHashSet<T> other);
    bool IsProperSupersetOf(ManagedHashSet<T> other);
    bool IsProperSubsetOf(ManagedHashSet<T> other);
    bool Overlaps(ManagedHashSet<T> other);
    bool SetEquals(ManagedHashSet<T> other);
}
