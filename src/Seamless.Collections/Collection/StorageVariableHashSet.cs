namespace Seamless.Collections;
public sealed class StorageManagedHashSet<T>:ManagedHashSet<T> where T:IEquatable<T>{
    public StorageManagedHashSet(StorageHashSetStore<T>Store):base(Store){}
    private protected override bool WouldOverflowMutable(int KeyLength,int ValueLength)=>false;
}
