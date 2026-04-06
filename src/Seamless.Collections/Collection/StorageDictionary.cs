namespace Seamless.Collections;
public sealed class StorageDictionary<TKey,TValue>:Dictionary<TKey,TValue>where TKey:IEquatable<TKey>where TValue:IEquatable<TValue>{
    public StorageDictionary(StorageDictionaryStore<TKey,TValue>Store):base(Store){}
    private protected override bool WouldOverflowMutable(int KeyLength,int ValueLength)=>false;
}
