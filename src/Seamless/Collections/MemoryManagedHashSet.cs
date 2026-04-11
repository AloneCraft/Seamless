using Seamless.Store;
namespace Seamless.Collections;
public sealed class MemoryManagedHashSet<T>:ManagedHashSet<T>where T:IEquatable<T>{
    internal MemoryManagedHashSet(MemoryManagedHashSetStore<T>CollectionStore):base(CollectionStore){}
    private protected override bool WouldOverflowMutable(int KeyLength,int ValueLength)=>this.PrivateProtectedWouldOverflowMutable(KeyLength, ValueLength);
}
