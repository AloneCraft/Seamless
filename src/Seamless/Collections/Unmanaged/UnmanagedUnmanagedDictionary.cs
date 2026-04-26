using Seamless.Stores;
namespace Seamless.Collections;
public sealed class UnmanagedUnmanagedDictionary<TKey, TValue, TKeySerializer, TValueSerializer>
    :UnmanagedDictionary<TKey,TValue,TKeySerializer,TValueSerializer>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue>
    where TKeySerializer : ISerializer<TKey>
    where TValueSerializer : ISerializer<TValue> {
    internal UnmanagedUnmanagedDictionary(UnmanagedCollectionStore<TKey,TValue> collectionStore) : base(collectionStore){
    }
}
