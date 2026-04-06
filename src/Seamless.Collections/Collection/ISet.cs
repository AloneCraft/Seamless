namespace Seamless.Collections;
public interface ISet<T> : ICollection<T>where T: IEquatable<T> {
    new bool Add(T item);
    void UnionWith(VariableHashSet<T> other);
    void IntersectWith(VariableHashSet<T> other);
    void ExceptWith(VariableHashSet<T> other);
    void SymmetricExceptWith(VariableHashSet<T> other);
    bool IsSubsetOf(VariableHashSet<T> other);
    bool IsSupersetOf(VariableHashSet<T> other);
    bool IsProperSupersetOf(VariableHashSet<T> other);
    bool IsProperSubsetOf(VariableHashSet<T> other);
    bool Overlaps(VariableHashSet<T> other);
    bool SetEquals(VariableHashSet<T> other);
}
