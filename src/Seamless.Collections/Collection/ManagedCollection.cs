using Garnet.common;
using MemoryPack;

namespace Seamless.Collections;

public abstract class ManagedCollection<TKey, TValue> : Collection<TKey, TValue, byte[], byte[]>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {

    private protected override ReadOnlySpan<byte> SerializeKey(ref TKey key)=>MemoryPackSerializer.Serialize(key);
    private protected override ReadOnlySpan<byte> SerializeValue(ref TValue value)=>MemoryPackSerializer.Serialize(value);

    protected ManagedCollection(CollectionStore<TKey, TValue> CollectionStore) : base(CollectionStore) { }
}
