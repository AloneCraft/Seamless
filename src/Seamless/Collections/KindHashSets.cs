using Seamless.Stores;
namespace Seamless.Collections;

public class ManagedUnmanagedHashSet<TKey>:ManagedHashSet<TKey>
    where TKey:IEquatable<TKey>{
    internal ManagedUnmanagedHashSet(ManagedCollectionStore<TKey,EqualityEmpty> collectionStore):base(collectionStore){
    }
}

public class UnmanagedUnmanagedHashSet<TKey>:UnmanagedHashSet<TKey>
    where TKey:unmanaged,IEquatable<TKey>{
    internal UnmanagedUnmanagedHashSet(UnmanagedCollectionStore<TKey,EqualityEmpty> collectionStore):base(collectionStore){
    }
}
