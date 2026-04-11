using Seamless.Store;
namespace Seamless.Collections;
public abstract partial class ManagedCollection<TKey, TValue, TKeySerializer, TValueSerializer>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
    where TKeySerializer : ISerializer<TKey>
    where TValueSerializer : ISerializer<TValue> {

    private protected ReadOnlySpan<byte> SerializeKey(ref TKey key) =>TKeySerializer.Serialize(ref key);
    private protected ReadOnlySpan<byte> SerializeValue(ref TValue value) => TValueSerializer.Serialize(ref value);

    private protected ManagedCollection(ManagedCollectionStore<TKey, TValue> CollectionStore) : base(CollectionStore) {
        //this.InitializeAddClientSession();
    }
}
