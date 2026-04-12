using Seamless.Stores;

using System.Diagnostics;
namespace Seamless.Collections;
[DebuggerTypeProxy(typeof(DebugView<>))]
public sealed class StorageManagedHashSet<TKey>:ManagedHashSet<TKey> where TKey:IEquatable<TKey>{
    public StorageManagedHashSet(StorageHashSetStore<TKey>Store):base(Store){}
    private protected override bool WouldOverflowMutable(int KeyLength,int ValueLength)=>false;
}
