namespace Seamless.Collections;
public sealed class StorageManagedDictionary<TKey,TValue>:ManagedDictionary<TKey,TValue>where TKey:IEquatable<TKey>where TValue:IEquatable<TValue>{
    public StorageManagedDictionary(StorageDictionaryStore<TKey,TValue>Store):base(Store){}
    private protected override bool WouldOverflowMutable(int KeyLength,int ValueLength)=>false;
}
