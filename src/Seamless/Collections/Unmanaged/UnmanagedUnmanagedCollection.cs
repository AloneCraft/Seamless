using Seamless.Stores;
namespace Seamless.Collections;

public abstract class UnmanagedUnmanagedCollection<TKey,TValue,TKeySerializer,TValueSerializer>
    :UnmanagedCollection<TKey,TValue,TKeySerializer,TValueSerializer>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue>
    where TKeySerializer:ISerializer<TKey>
    where TValueSerializer:ISerializer<TValue>{
    private protected UnmanagedUnmanagedCollection(UnmanagedCollectionStore<TKey,TValue> CollectionStore):base(CollectionStore){
    }
}
