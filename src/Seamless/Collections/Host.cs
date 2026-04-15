namespace Seamless.Collections;

public interface ICollectionStrategyHost<T> : ICollection<T> {
    ICollection<T> CollectionCore { get; }
}

public interface IDictionaryStrategyHost<TKey, TValue>
    : ISeamlessDictionary<TKey,TValue>, ICollectionStrategyHost<KeyValuePair<TKey,TValue>>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    ISeamlessDictionary<TKey,TValue> DictionaryCore { get; }
}

public interface IHashSetStrategyHost<TKey>
    : IHashSet<TKey,IHashSetStrategyHost<TKey>>, ICollectionStrategyHost<TKey>, IDisposable
    where TKey : IEquatable<TKey> {
    object HashSetCore { get; }
}