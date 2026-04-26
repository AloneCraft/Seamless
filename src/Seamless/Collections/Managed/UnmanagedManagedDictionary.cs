using Seamless.Stores;
namespace Seamless.Collections;
public sealed partial class UnmanagedManagedDictionary<TKey, TValue, TKeySerializer, TValueSerializer>
    :ManagedDictionary<TKey,TValue,TKeySerializer,TValueSerializer>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : IEquatable<TValue>
    where TKeySerializer : ISerializer<TKey>
    where TValueSerializer : ISerializer<TValue> {
    internal UnmanagedManagedDictionary(ManagedCollectionStore<TKey,TValue> collectionStore) : base(collectionStore){
    }
}
