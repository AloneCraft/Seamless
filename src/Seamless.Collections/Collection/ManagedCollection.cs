using Seamless.SessionFunctions;
using Seamless.Store;
namespace Seamless.Collections;
public abstract partial class ManagedCollection<TKey, TValue, TKeySerializer, TValueSerializer>
    : Collection<TKey, TValue,
#if TSAVORITE1_1_1
        ManagedSessionKey, ManagedSessionValue,
        ManagedSessionKey, ManagedOutput,
#endif
        Add, Delete,
        ManagedStoreFunctions, ManagedAllocator>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
    where TKeySerializer : ISerializer<TKey>
    where TValueSerializer : ISerializer<TValue> {

    private protected override ReadOnlySpan<byte> SerializeKey(ref TKey key) => TKeySerializer.Serialize(ref key);
    private protected override ReadOnlySpan<byte> SerializeValue(ref TValue value) => TValueSerializer.Serialize(ref value);

    protected ManagedCollection(ManagedCollectionStore<TKey, TValue> CollectionStore) : base(CollectionStore) { }
}
