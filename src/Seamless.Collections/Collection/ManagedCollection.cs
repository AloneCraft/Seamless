using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;

using System.Diagnostics;
using Seamless.Store;
namespace Seamless.Collections;
public abstract partial class ManagedCollection<TKey, TValue, TKeySerializer, TValueSerializer>
    : Collection<TKey, TValue,
        ManagedSessionKey, ManagedSessionValue,
        ManagedSessionKey, ManagedOutput,
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
