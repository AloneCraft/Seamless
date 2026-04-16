using Seamless.Stores;
namespace Seamless.Collections;

public interface IKeyKind;
public interface IValueKind;

public readonly struct ManagedKey:IKeyKind;
public readonly struct UnmanagedKey:IKeyKind;

public readonly struct ManagedValue:IValueKind;
public readonly struct UnmanegedValue:IValueKind;

public abstract class ManagedManagedCollection<TKey,TValue,TKeySerializer,TValueSerializer>
    :ManagedCollection<TKey,TValue,TKeySerializer,TValueSerializer>
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>
    where TKeySerializer:ISerializer<TKey>
    where TValueSerializer:ISerializer<TValue>{
    private protected ManagedManagedCollection(ManagedCollectionStore<TKey,TValue> CollectionStore):base(CollectionStore){
    }
}

public abstract partial class UnmanagedManagedCollection<TKey,TValue,TKeySerializer,TValueSerializer>
    :ManagedCollection<TKey,TValue,TKeySerializer,TValueSerializer>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:IEquatable<TValue>
    where TKeySerializer:ISerializer<TKey>
    where TValueSerializer:ISerializer<TValue>{
    private protected UnmanagedManagedCollection(ManagedCollectionStore<TKey,TValue> CollectionStore):base(CollectionStore){
        this.InitializeOptimized(CollectionStore);
    }
    partial void InitializeOptimized(ManagedCollectionStore<TKey,TValue> CollectionStore);
}

public abstract partial class ManagedUnmanagedCollection<TKey,TValue,TKeySerializer,TValueSerializer>
    :ManagedCollection<TKey,TValue,TKeySerializer,TValueSerializer>
    where TKey:IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue>
    where TKeySerializer:ISerializer<TKey>
    where TValueSerializer:ISerializer<TValue>{
    private protected ManagedUnmanagedCollection(ManagedCollectionStore<TKey,TValue> CollectionStore):base(CollectionStore){
        this.InitializeOptimized(CollectionStore);
    }
    partial void InitializeOptimized(ManagedCollectionStore<TKey,TValue> CollectionStore);
}

public abstract class UnmanagedUnmanagedCollection<TKey,TValue,TKeySerializer,TValueSerializer>
    :UnmanagedCollection<TKey,TValue,TKeySerializer,TValueSerializer>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue>
    where TKeySerializer:ISerializer<TKey>
    where TValueSerializer:ISerializer<TValue>{
    private protected UnmanagedUnmanagedCollection(UnmanagedCollectionStore<TKey,TValue> CollectionStore):base(CollectionStore){
    }
}
