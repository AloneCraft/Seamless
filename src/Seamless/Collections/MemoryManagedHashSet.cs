using Seamless.Stores;

using System.Diagnostics;
namespace Seamless.Collections;
[DebuggerTypeProxy(typeof(DebugView<>))]
public sealed class MemoryManagedHashSet<TKey>:ManagedHashSet<TKey>where TKey:IEquatable<TKey>{
    internal MemoryManagedHashSet(MemoryManagedHashSetStore<TKey>CollectionStore):base(CollectionStore){}
    private protected override bool WouldOverflowMutable(int KeyLength,int ValueLength)=>this.PrivateProtectedWouldOverflowMutable(KeyLength, ValueLength);
}
