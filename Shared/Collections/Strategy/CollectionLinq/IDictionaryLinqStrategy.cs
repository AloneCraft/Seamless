namespace Collection.Strategy.CollectionLinq;

public interface IDictionaryLinqStrategy<TSource, TElement, TKey, TValue>:ICollectionLinqStrategy<TSource,TElement,TKey,TValue>
    where TSource : IDisposable
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    static abstract IEnumerable<TKey> Keys(TSource source);
    static abstract IEnumerable<TValue> Values(TSource source);
}
