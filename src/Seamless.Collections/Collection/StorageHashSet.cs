namespace Seamless.Collections;
public sealed class StorageHashSet<T>:HashSet<T> where T:IEquatable<T>{
    public StorageHashSet(StorageHashSetStore<T>Store):base(Store){}
    private protected override bool WouldOverflowMutable(int KeyLength,int ValueLength)=>false;
}
