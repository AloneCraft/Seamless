using Seamless.Stores;
namespace Seamless.Collections;
public abstract partial class UnmanagedManagedCollection<TKey,TValue,TKeySerializer,TValueSerializer>
    :ManagedCollection<TKey,TValue,TKeySerializer,TValueSerializer>
    where TKey:unmanaged    where TKeySerializer:ISerializer<TKey>
    where TValueSerializer:ISerializer<TValue>{
    private protected UnmanagedManagedCollection(ManagedCollectionStore<TKey,TValue> CollectionStore):base(CollectionStore){
        this.InitializeOptimized(CollectionStore);
    }
    partial void InitializeOptimized(ManagedCollectionStore<TKey,TValue> CollectionStore);
}
