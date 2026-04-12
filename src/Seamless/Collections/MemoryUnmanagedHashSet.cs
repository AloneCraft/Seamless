using Seamless.Stores;

using System.Diagnostics;
namespace Seamless.Collections;
[DebuggerTypeProxy(typeof(DebugView<>))]
public sealed class MemoryUnmanagedHashSet<TKey>:UnmanagedHashSet<TKey>
    where TKey:unmanaged,IEquatable<TKey> {
    internal MemoryUnmanagedHashSet(UnmanagedCollectionStore<TKey,EqualityEmpty> CollectionStore):base(CollectionStore){}
    private protected override bool WouldOverflowMutable(int KeyLength,int ValueLength)=>this.PrivateProtectedWouldOverflowMutable(KeyLength, ValueLength);
}
