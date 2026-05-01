using Seamless.Stores;
namespace Seamless.Collections;
public abstract partial class ManagedUnmanagedCollection<TKey,TValue,TKeySerializer,TValueSerializer>
    :ManagedCollection<TKey,TValue,TKeySerializer,TValueSerializer>    where TValue:unmanaged
    where TKeySerializer:ISerializer<TKey>
    where TValueSerializer:ISerializer<TValue>{
    private protected ManagedUnmanagedCollection(ManagedCollectionStore<TKey,TValue> CollectionStore):base(CollectionStore){
        this.InitializeOptimized(CollectionStore);
    }
    partial void InitializeOptimized(ManagedCollectionStore<TKey,TValue> CollectionStore);
}
