using Seamless.Stores;
namespace Seamless.Collections;
public sealed partial class ManagedUnmanagedDictionary<TKey, TValue, TKeySerializer, TValueSerializer>
    :ManagedDictionary<TKey,TValue,TKeySerializer,TValueSerializer>    where TValue : unmanaged
    where TKeySerializer : ISerializer<TKey>
    where TValueSerializer : ISerializer<TValue> {
    internal ManagedUnmanagedDictionary(ManagedCollectionStore<TKey,TValue> collectionStore) : base(collectionStore){
        this.InitializeKind(collectionStore);
    }
    partial void InitializeKind(ManagedCollectionStore<TKey,TValue> collectionStore);
    partial void DisposeKind(bool disposing);
    protected override void Dispose(bool disposing){
        this.DisposeKind(disposing);
        base.Dispose(disposing);
    }
}
