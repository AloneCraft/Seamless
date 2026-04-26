using Seamless.Collections;

namespace Seamless.Stores;

public static class ManagedDictionaryStoreExtensions {
    public static ManagedManagedDictionary<TKey,TValue,ManagedSerializer<TKey>,ManagedSerializer<TValue>> Create<TKey, TValue>(
        this ManagedDictionaryStore<TKey,TValue> store
    )
        where TKey : IEquatable<TKey>
        where TValue : IEquatable<TValue> {
        return new ManagedManagedDictionary<TKey,TValue,ManagedSerializer<TKey>,ManagedSerializer<TValue>>(store);
    }

    public static ManagedManagedDictionary<TKey,TValue,ManagedSerializer<TKey>,ManagedSerializer<TValue>> CreateManagedManaged<TKey, TValue>(
        this ManagedDictionaryStore<TKey,TValue> store
    )
        where TKey : IEquatable<TKey>
        where TValue : IEquatable<TValue> {
        return new ManagedManagedDictionary<TKey,TValue,ManagedSerializer<TKey>,ManagedSerializer<TValue>>(store);
    }

    public static UnmanagedManagedDictionary<TKey,TValue,UnmanagedSerializer<TKey>,ManagedSerializer<TValue>> CreateUnmanagedManaged<TKey, TValue>(
        this ManagedDictionaryStore<TKey,TValue> store
    )
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : IEquatable<TValue> {
        return new UnmanagedManagedDictionary<TKey,TValue,UnmanagedSerializer<TKey>,ManagedSerializer<TValue>>(store);
    }

    public static ManagedUnmanagedDictionary<TKey,TValue,ManagedSerializer<TKey>,UnmanagedSerializer<TValue>> CreateManagedUnmanaged<TKey, TValue>(
        this ManagedDictionaryStore<TKey,TValue> store
    )
        where TKey : IEquatable<TKey>
        where TValue : unmanaged, IEquatable<TValue> {
        return new ManagedUnmanagedDictionary<TKey,TValue,ManagedSerializer<TKey>,UnmanagedSerializer<TValue>>(store);
    }
}
