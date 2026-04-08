namespace Seamless.Collections;
public sealed class StorageManagedDictionary<TKey,TValue,TKeySerializer,TValueSerializer>:ManagedDictionary<TKey,TValue,TKeySerializer,TValueSerializer>
    where TKey:IEquatable<TKey>where TValue:IEquatable<TValue>
    where TKeySerializer:ISerializer<TKey>where TValueSerializer:ISerializer<TValue>{
    public StorageManagedDictionary(ManagedCollectionStore<TKey,TValue>Store):base(Store){}
    private protected override bool WouldOverflowMutable(int KeyLength,int ValueLength)=>false;
}
