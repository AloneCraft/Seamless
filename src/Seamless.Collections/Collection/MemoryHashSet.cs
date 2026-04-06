namespace Seamless.Collections;
public sealed class MemoryHashSet<T>:HashSet<T>where T:IEquatable<T>{
    internal MemoryHashSet(MemoryHashSetStore<T>CollectionStore):base(CollectionStore){}
    private protected override bool WouldOverflowMutable(int KeyLength,int ValueLength)=>this.PrivateProtectedWouldOverflowMutable(KeyLength, ValueLength);
}
