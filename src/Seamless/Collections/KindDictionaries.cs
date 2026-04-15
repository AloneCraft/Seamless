using Seamless.Stores;
namespace Seamless.Collections;

public class ManagedManagedDictionary<TKey,TValue,TKeySerializer,TValueSerializer>
    :ManagedDictionary<TKey,TValue,TKeySerializer,TValueSerializer>
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>
    where TKeySerializer:ISerializer<TKey>
    where TValueSerializer:ISerializer<TValue>{
    internal ManagedManagedDictionary(ManagedCollectionStore<TKey,TValue> collectionStore):base(collectionStore){
    }
}

public class UnmanagedManagedDictionary<TKey,TValue,TKeySerializer,TValueSerializer>
    :ManagedDictionary<TKey,TValue,TKeySerializer,TValueSerializer>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:IEquatable<TValue>
    where TKeySerializer:ISerializer<TKey>
    where TValueSerializer:ISerializer<TValue>{
    internal UnmanagedManagedDictionary(ManagedCollectionStore<TKey,TValue> collectionStore):base(collectionStore){
    }
}

public class ManagedUnmanagedDictionary<TKey,TValue,TKeySerializer,TValueSerializer>
    :ManagedDictionary<TKey,TValue,TKeySerializer,TValueSerializer>
    where TKey:IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue>
    where TKeySerializer:ISerializer<TKey>
    where TValueSerializer:ISerializer<TValue>{
    internal ManagedUnmanagedDictionary(ManagedCollectionStore<TKey,TValue> collectionStore):base(collectionStore){
    }
}

public class UnmanagedUnmanagedDictionary<TKey,TValue,TKeySerializer,TValueSerializer>
    :UnmanagedDictionary<TKey,TValue,TKeySerializer,TValueSerializer>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue>
    where TKeySerializer:ISerializer<TKey>
    where TValueSerializer:ISerializer<TValue>{
    internal UnmanagedUnmanagedDictionary(UnmanagedCollectionStore<TKey,TValue> collectionStore):base(collectionStore){
    }
}
