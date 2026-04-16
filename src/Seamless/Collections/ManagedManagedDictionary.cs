using Seamless.Stores;
namespace Seamless.Collections;

public sealed class ManagedManagedDictionary<TKey, TValue, TKeySerializer, TValueSerializer>
    :ManagedDictionary<TKey,TValue,TKeySerializer,TValueSerializer>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
    where TKeySerializer : ISerializer<TKey>
    where TValueSerializer : ISerializer<TValue> {
    internal ManagedManagedDictionary(ManagedCollectionStore<TKey,TValue> collectionStore) : base(collectionStore){
    }
}
