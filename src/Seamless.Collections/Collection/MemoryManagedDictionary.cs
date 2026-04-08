namespace Seamless.Collections;
public sealed class MemoryManagedDictionary<TKey,TValue,TKeySerializer,TValueSerializer>:ManagedDictionary<TKey,TValue,TKeySerializer,TValueSerializer>
    where TKey:IEquatable<TKey>where TValue:IEquatable<TValue>
    where TKeySerializer:ISerializer<TKey>where TValueSerializer:ISerializer<TValue>{
    internal MemoryManagedDictionary(ManagedCollectionStore<TKey,TValue>CollectionStore):base(CollectionStore){}
    private protected override bool WouldOverflowMutable(int KeyLength,int ValueLength)=>this.PrivateProtectedWouldOverflowMutable(KeyLength, ValueLength);
}
