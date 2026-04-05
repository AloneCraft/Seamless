namespace Seamless.Collections;
public sealed class MemoryDictionary<TKey,TValue>:Dictionary<TKey,TValue>where TKey:IEquatable<TKey>where TValue:IEquatable<TValue>{
    public MemoryDictionary(MemoryDictionaryStore<TKey,TValue>CollectionStore):base(CollectionStore){
    }
    private protected override bool WouldOverflowMutable(int KeyLength,int ValueLength)=>this.PrivateProtectedWouldOverflowMutable(KeyLength, ValueLength);
}
