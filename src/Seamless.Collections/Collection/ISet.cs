namespace Seamless.Collections;
public interface ISet<T> : ICollection<T>where T: IEquatable<T> {
    new bool Add(T item);
    void UnionWith(HashSet<T> other);
    void IntersectWith(HashSet<T> other);
    void ExceptWith(HashSet<T> other);
    void SymmetricExceptWith(HashSet<T> other);
    bool IsSubsetOf(HashSet<T> other);
    bool IsSupersetOf(HashSet<T> other);
    bool IsProperSupersetOf(HashSet<T> other);
    bool IsProperSubsetOf(HashSet<T> other);
    bool Overlaps(HashSet<T> other);
    bool SetEquals(HashSet<T> other);
}
