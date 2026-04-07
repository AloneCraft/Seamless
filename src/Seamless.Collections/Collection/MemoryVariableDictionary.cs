namespace Seamless.Collections;
public sealed class MemoryManagedDictionary<TKey,TValue>:ManagedDictionary<TKey,TValue>
    where TKey:IEquatable<TKey>where TValue:IEquatable<TValue>{
    internal MemoryManagedDictionary(MemoryDictionaryStore<TKey,TValue>CollectionStore):base(CollectionStore){}
    private protected override bool WouldOverflowMutable(int KeyLength,int ValueLength)=>this.PrivateProtectedWouldOverflowMutable(KeyLength, ValueLength);
}
