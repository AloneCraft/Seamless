namespace Seamless.Collections;
public sealed class MemoryHashSet<T>(MemoryHashSetStore<T>CollectionStore):HashSet<T>(CollectionStore)where T:IEquatable<T>{
    private protected override bool WouldOverflowMutable(int KeyLength,int ValueLength)=>this.PrivateProtectedWouldOverflowMutable(KeyLength, ValueLength);
}
