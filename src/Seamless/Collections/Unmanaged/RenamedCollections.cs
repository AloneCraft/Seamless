using Seamless.Stores;

namespace Seamless.Collections;

public abstract partial class Collection<TKey,TValue,TKeySerializer,TValueSerializer>
    :UnmanagedCollection<TKey,TValue,TKeySerializer,TValueSerializer>
    where TKey : unmanaged
    where TValue : unmanaged
    where TKeySerializer : ISerializer<TKey>
    where TValueSerializer : ISerializer<TValue> {
    private protected Collection(UnmanagedCollectionStore<TKey,TValue> collectionStore) : base(collectionStore) {
    }
}

public class Dictionary<TKey,TValue,TKeySerializer,TValueSerializer>
    :UnmanagedUnmanagedDictionary<TKey,TValue,TKeySerializer,TValueSerializer>
    where TKey : unmanaged
    where TValue : unmanaged
    where TKeySerializer : ISerializer<TKey>
    where TValueSerializer : ISerializer<TValue> {
    internal Dictionary(UnmanagedCollectionStore<TKey,TValue> collectionStore) : base(collectionStore) {
    }
}

public class HashSet<TKey>:UnmanagedHashSet<TKey>
    where TKey : unmanaged {
    internal HashSet(UnmanagedCollectionStore<TKey,EqualityEmpty> collectionStore,IDisposable? ownedStore = null) : base(collectionStore,ownedStore) {
    }
}

public class Set<TKey,TRecord>:UnmanagedSet<TKey,TRecord>
    where TRecord : unmanaged, IRecord<TKey>
    where TKey : unmanaged {
    internal Set(UnmanagedCollectionStore<TKey,TRecord> collectionStore,IDisposable? ownedStore = null) : base(collectionStore,ownedStore) {
    }
}

public class Set<TRecord>:UnmanagedSet<TRecord>
    where TRecord : unmanaged {
    public Set(string path,long initialCapacity = 16) : base(path,initialCapacity) {
    }
}
