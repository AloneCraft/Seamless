using Seamless.Collections.Tsavorite;

namespace Test.Seamless.Stores;

public class StoreTests {
    [Fact, Trait("Category","Unit")]
    public void Create_UnmanagedKey_UnmanagedValue_ReturnsUnmanagedDictionaryStore() {
        using var store = Store.Create().Create<int,long>();
        Assert.IsType<DictionaryStore<int,long>>(store);
    }

    [Fact, Trait("Category","Unit")]
    public void Create_UnmanagedKey_ManagedValue_ReturnsUnmanagedAndMemoryPackSerializers() {
        using var store = Store.Create().Create<int,string>();
        Assert.IsType<DictionaryStore<int,string>>(store);
    }

    [Fact, Trait("Category","Unit")]
    public void Create_ManagedKey_UnmanagedValue_ReturnsManagedAndBlittableSerializers() {
        using var store = Store.Create().Create<string,int>();
        Assert.IsType<DictionaryStore<string,int>>(store);
    }

    [Fact, Trait("Category","Unit")]
    public void Create_ManagedKey_ManagedValue_ReturnsMemoryPackSerializer() {
        using var store = Store.Create().Create<string,string>();
        Assert.IsType<DictionaryStore<string,string>>(store);
    }

    [Fact, Trait("Category","Unit")]
    public void Create_MemoryPackSerializerStrategyExplicit_UnmanagedKey_UnmanagedValue_ReturnsMemoryPackSerializer() {
        using var store = Store.Create().Create<int,int>(new MemoryPackSerializerStrategy(),new MemoryPackSerializerStrategy());
        Assert.IsType<DictionaryStore<int,int>>(store);
    }

    [Fact, Trait("Category","Unit")]
    public void Create_UnmanagedStruct_UnmanagedStruct_ReturnsUnmanagedDictionaryStore() {
        using var store = Store.Create().Create<UnmanagedStruct,UnmanagedStruct>();
        Assert.IsType<DictionaryStore<UnmanagedStruct,UnmanagedStruct>>(store);
    }

    [Fact, Trait("Category","Unit")]
    public void Create_ManagedStruct_ManagedStruct_ReturnsMemoryPackSerializer() {
        using var store = Store.Create().Create<ManagedStruct,ManagedStruct>();
        Assert.IsType<DictionaryStore<ManagedStruct,ManagedStruct>>(store);
    }

    [Fact, Trait("Category","Unit")]
    public void Create_UnmanagedStruct_ManagedStruct_ReturnsUnmanagedAndMemoryPackSerializers() {
        using var store = Store.Create().Create<UnmanagedStruct,ManagedStruct>();
        Assert.IsType<DictionaryStore<UnmanagedStruct,ManagedStruct>>(store);
    }

    [Fact, Trait("Category","Unit")]
    public void Create_ManagedStruct_UnmanagedStruct_ReturnsManagedAndBlittableSerializers() {
        using var store = Store.Create().Create<ManagedStruct,UnmanagedStruct>();
        Assert.IsType<DictionaryStore<ManagedStruct,UnmanagedStruct>>(store);
    }

    [Fact, Trait("Category","Unit")]
    public void CreateManagedManaged_ReturnsManagedManagedDictionary() {
        using var store = Store.Create().Create<string,string>();
        using var dictionary = store.Create();
        Assert.IsType<Dictionary<string,string,MemoryPackSerializer<string>,MemoryPackSerializer<string>>>(dictionary);
    }

    [Fact, Trait("Category","Unit")]
    public void CreateUnmanagedManaged_ReturnsUnmanagedManagedDictionary() {
        using var store = Store.Create().Create<int,string>();
        using var dictionary = store.Create();
        Assert.IsType<Dictionary<int,string,BlittableSerializer<int>,MemoryPackSerializer<string>>>(dictionary);
    }

    [Fact, Trait("Category","Unit")]
    public void CreateManagedUnmanaged_ReturnsManagedUnmanagedDictionary() {
        using var store = Store.Create().Create<string,int>();
        using var dictionary = store.Create();
        Assert.IsType<Dictionary<string,int,MemoryPackSerializer<string>,BlittableSerializer<int>>>(dictionary);
    }

    [Fact, Trait("Category","Unit")]
    public void CreateUnmanagedUnmanaged_ReturnsUnmanagedUnmanagedDictionary() {
        using var store = Store.Create().Create<int,int>();
        using var dictionary = store.Create();
        Assert.IsType<Dictionary<int,int,BlittableSerializer<int>,BlittableSerializer<int>>>(dictionary);
    }

    [Fact, Trait("Category","Unit")]
    public void CreateManagedUnmanagedHashSet_ReturnsManagedUnmanagedHashSet() {
        using var store = Store.Create().Create<string>(new MemoryPackSerializerStrategy());
        using var set = store.Create();
        Assert.IsType<global::Seamless.Collections.Tsavorite.HashSet<string>>(set);
    }

    [Fact, Trait("Category","Unit")]
    public void CreateUnmanagedUnmanagedHashSet_ReturnsUnmanagedUnmanagedHashSet() {
        using var store = Store.Create().Create<int>();
        using var set = store.Create();
        Assert.IsType<global::Seamless.Collections.Tsavorite.HashSet<int>>(set);
    }

    [Fact, Trait("Category","Unit")]
    public void Create_StorageLimitOptions_PassedToStorageStore() {
        using var logDevice = new global::Seamless.Collections.Tsavorite.MemoryOnlyDevice(global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity);
        using var objectLogDevice = new global::Seamless.Collections.Tsavorite.MemoryOnlyDevice(global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity);
        using var store = Store.Create(
            MemorySize: global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity,
            StorageLimitFraction: 0.5,
            StorageReserveBytes: 1024
        ).Create<int,int>(logDevice,objectLogDevice);

        Assert.Equal(global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity/2,store.StorageOverflowMutableStrategy.LimitBytes);
    }

    [Fact, Trait("Category","Unit")]
    public void Create_InvalidStorageLimitOptions_Throws() {
        Assert.Throws<ArgumentOutOfRangeException>(() => Store.Create(StorageLimitFraction: -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Store.Create(StorageLimitFraction: 1.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Store.Create(StorageLimitFraction: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => Store.Create(StorageReserveBytes: -1));
    }

    [Fact, Trait("Category","Unit")]
    public void Create_InvalidTsavoriteSizeOptions_Throws() {
        Assert.Throws<ArgumentOutOfRangeException>(() => Store.Create(IndexSize: 63).Create<int,int>());
        Assert.Throws<ArgumentOutOfRangeException>(() => Store.Create(PageSize: 2048).Create<int,int>());
        Assert.Throws<ArgumentOutOfRangeException>(() => Store.Create(PageSize: 1L<<13,SegmentSize: 1L<<12).Create<int,int>());
        Assert.Throws<ArgumentOutOfRangeException>(() => Store.Create(PageSize: 1L<<12,MemorySize: 1L<<12).Create<int,int>());
        Assert.Throws<ArgumentOutOfRangeException>(() => Store.Create(MutableFraction: -0.1).Create<int,int>());
        Assert.Throws<ArgumentOutOfRangeException>(() => Store.Create(MutableFraction: 1.1).Create<int,int>());
        Assert.Throws<ArgumentOutOfRangeException>(() => Store.Create(MutableFraction: double.NaN).Create<int,int>());
    }
}
