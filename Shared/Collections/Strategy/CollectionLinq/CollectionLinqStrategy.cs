namespace Collection.Strategy.CollectionLinq;

public interface ICollectionLinqStrategy<TSource,TElement,TKey,TValue>
    where TSource : IDisposable
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
{
    static abstract TSource Create(int count);
    static abstract TSource CreateSecond(int count);
    static abstract IEnumerable<TElement> AsEnumerable(TSource source);
    static abstract TKey Key(TElement element);
    static abstract TValue Value(TElement element);
    static abstract int Number(TElement element);
    static abstract int KeyNumber(TKey key);
    static abstract int ValueNumber(TValue value);
}

public interface IDictionaryLinqStrategy<TSource,TElement,TKey,TValue>:ICollectionLinqStrategy<TSource,TElement,TKey,TValue>
    where TSource : IDisposable
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
{
    static abstract IEnumerable<TKey> Keys(TSource source);
    static abstract IEnumerable<TValue> Values(TSource source);
}
