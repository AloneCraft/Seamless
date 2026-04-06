namespace Seamless.Collections;
public sealed class MemoryVariableHashSet<T>:VariableHashSet<T>where T:IEquatable<T>{
    internal MemoryVariableHashSet(MemoryHashSetStore<T>CollectionStore):base(CollectionStore){}
    private protected override bool WouldOverflowMutable(int KeyLength,int ValueLength)=>this.PrivateProtectedWouldOverflowMutable(KeyLength, ValueLength);
}
