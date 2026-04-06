namespace Seamless.Collections;
public sealed class MemoryVariableDictionary<TKey,TValue>:VariableDictionary<TKey,TValue>
    where TKey:IEquatable<TKey>where TValue:IEquatable<TValue>{
    internal MemoryVariableDictionary(MemoryDictionaryStore<TKey,TValue>CollectionStore):base(CollectionStore){}
    private protected override bool WouldOverflowMutable(int KeyLength,int ValueLength)=>this.PrivateProtectedWouldOverflowMutable(KeyLength, ValueLength);
}
