namespace Seamless.Collections;

public readonly struct UnmanagedKeyComparer<TKey>:IKeyComparer<TKey>
    where TKey:unmanaged,IEquatable<TKey>{
    public bool Equals(ref TKey k1,ref TKey k2)=>k1.Equals(k2);
    public long GetHashCode64(ref TKey key)=>key.GetHashCode();
}
