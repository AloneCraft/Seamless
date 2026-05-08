using Seamless.Collections;

namespace Seamless.Stores;

public static class DictionaryStoreExtensions {
    public static Dictionary<TKey,TValue,MemoryPackSerializer<TKey>,MemoryPackSerializer<TValue>> Create<TKey, TValue>(
        this DictionaryStore<TKey,TValue> store
    ) {
        return new Dictionary<TKey,TValue,MemoryPackSerializer<TKey>,MemoryPackSerializer<TValue>>(store);
    }

    public static Dictionary<TKey,TValue,MemoryPackSerializer<TKey>,MemoryPackSerializer<TValue>> CreateMemoryPackMemoryPack<TKey, TValue>(
        this DictionaryStore<TKey,TValue> store
    ) {
        return new Dictionary<TKey,TValue,MemoryPackSerializer<TKey>,MemoryPackSerializer<TValue>>(store);
    }

    public static Dictionary<TKey,TValue,BlittableSerializer<TKey>,MemoryPackSerializer<TValue>> CreateBlittableMemoryPack<TKey, TValue>(
        this DictionaryStore<TKey,TValue> store
    )
        where TKey : unmanaged {
        return new Dictionary<TKey,TValue,BlittableSerializer<TKey>,MemoryPackSerializer<TValue>>(store);
    }

    public static Dictionary<TKey,TValue,MemoryPackSerializer<TKey>,BlittableSerializer<TValue>> CreateMemoryPackBlittable<TKey, TValue>(
        this DictionaryStore<TKey,TValue> store
    )        where TValue : unmanaged {
        return new Dictionary<TKey,TValue,MemoryPackSerializer<TKey>,BlittableSerializer<TValue>>(store);
    }
}
