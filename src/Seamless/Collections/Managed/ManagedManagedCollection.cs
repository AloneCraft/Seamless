using Seamless.Stores;
namespace Seamless.Collections;

public abstract class ManagedManagedCollection<TKey,TValue,TKeySerializer,TValueSerializer>
    :ManagedCollection<TKey,TValue,TKeySerializer,TValueSerializer>    where TKeySerializer:ISerializer<TKey>
    where TValueSerializer:ISerializer<TValue>{
    private protected ManagedManagedCollection(ManagedCollectionStore<TKey,TValue> CollectionStore):base(CollectionStore){
    }
}
