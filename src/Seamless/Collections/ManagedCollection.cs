using Seamless.ScanIteratorFunctions;
using Seamless.Stores;
using System.Runtime.InteropServices;
namespace Seamless.Collections;

public abstract partial class ManagedCollection<TKey,TValue,TKeySerializer,TValueSerializer>
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>
    where TKeySerializer:ISerializer<TKey>
    where TValueSerializer:ISerializer<TValue>{
    public sealed partial class KeyIterator:ScanIteratorFunctions<TKey>;
    internal sealed partial class ValueIterator:ScanIteratorFunctions<TValue>;
    public sealed partial class KeyValueIterator:ScanIteratorFunctions<KeyValuePair<TKey,TValue>>;
    [StructLayout(LayoutKind.Auto)]public partial struct Enumerator<TIterator,T>
        where TIterator:ScanIteratorFunctions<T>;
    private protected ManagedCollection(ManagedCollectionStore<TKey,TValue> CollectionStore):base(CollectionStore){
        //this.InitializeAddClientSession();
    }
    public sealed partial class CounterIterator : CounterIteratorBase;
}
