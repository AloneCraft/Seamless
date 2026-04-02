namespace Seamless.Collections;
//using System.Collections;
public interface IRelation<T> : IEnumerable<T>where T: IEquatable<T> {
    bool Add(T item);
}
