using Seamless.Stores;
namespace Seamless.Collections;

public abstract partial class UnmanagedCollection<TKey,TValue,TKeySerializer,TValueSerializer>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue>
    where TKeySerializer:ISerializer<TKey>
    where TValueSerializer:ISerializer<TValue>{
    public sealed partial class KeyIterator;
    internal sealed partial class ValueIterator;
    public sealed partial class KeyValueIterator;
    protected UnmanagedCollection(UnmanagedCollectionStore<TKey,TValue> CollectionStore):base(CollectionStore){
    }
    //public sealed partial class CounterIterator:CounterIteratorBase;
}
