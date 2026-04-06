namespace Seamless.Collections;
public sealed class StorageVariableHashSet<T>:VariableHashSet<T> where T:IEquatable<T>{
    public StorageVariableHashSet(StorageHashSetStore<T>Store):base(Store){}
    private protected override bool WouldOverflowMutable(int KeyLength,int ValueLength)=>false;
}
