namespace Seamless.Collections;
public sealed class StorageVariableDictionary<TKey,TValue>:VariableDictionary<TKey,TValue>where TKey:IEquatable<TKey>where TValue:IEquatable<TValue>{
    public StorageVariableDictionary(StorageDictionaryStore<TKey,TValue>Store):base(Store){}
    private protected override bool WouldOverflowMutable(int KeyLength,int ValueLength)=>false;
}
