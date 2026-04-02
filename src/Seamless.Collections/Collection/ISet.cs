namespace Seamless.Collections;
//using System.Collections;
public interface ISet<T> : ICollection<T>where T: IEquatable<T> {
    //Add ITEM to the set, return true if added, false if duplicate
    new bool Add(T item);

    //Transform this set into its union with the IEnumerable<T> other
    void UnionWith(HashSet<T> other);

    //Transform this set into its intersection with the IEnumerable<T> other
    void IntersectWith(HashSet<T> other);

    //Transform this set so it contains no elements that are also in other
    void ExceptWith(HashSet<T> other);

    //Transform this set so it contains elements initially in this or in other, but not both
    void SymmetricExceptWith(HashSet<T> other);

    //Check if this set is a subset of other
    bool IsSubsetOf(HashSet<T> other);

    //Check if this set is a superset of other
    bool IsSupersetOf(HashSet<T> other);

    //Check if this set is a subset of other, but not the same as it
    bool IsProperSupersetOf(HashSet<T> other);

    //Check if this set is a superset of other, but not the same as it
    bool IsProperSubsetOf(HashSet<T> other);

    //Check if this set has any elements in common with other
    bool Overlaps(HashSet<T> other);

    //Check if this set contains the same and only the same elements as other
    bool SetEquals(HashSet<T> other);
}
