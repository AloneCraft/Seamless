using Seamless.Collections;
using Seamless.Stores;
using Test.Seamless.Collections;
namespace Test.Seamless.Unit.Stores;

public class HashStoreTests {
    [Fact, Trait("Category","Unit")]
    public void Create_UnmanagedKey_UnmanagedValue_ReturnsUnmanagedDictionaryStore() {
        using var store = HashStore.Create().Create<int,long>();
        Assert.IsType<UnmanagedDictionaryStore<int,long>>(store);
    }

    [Fact, Trait("Category","Unit")]
    public void Create_UnmanagedKey_ManagedValue_ReturnsUnmanagedAndManagedSerializers() {
        using var store = HashStore.Create().Create<int,string>();
        Assert.IsType<ManagedDictionaryStore<int,string>>(store);
    }

    [Fact, Trait("Category","Unit")]
    public void Create_ManagedKey_UnmanagedValue_ReturnsManagedAndUnmanagedSerializers() {
        using var store = HashStore.Create().Create<string,int>();
        Assert.IsType<ManagedDictionaryStore<string,int>>(store);
    }

    [Fact, Trait("Category","Unit")]
    public void Create_ManagedKey_ManagedValue_ReturnsManagedSerializer() {
        using var store = HashStore.Create().Create<string,string>();
        Assert.IsType<ManagedDictionaryStore<string,string>>(store);
    }

    [Fact, Trait("Category","Unit")]
    public void Create_ManagedTagExplicit_UnmanagedKey_UnmanagedValue_ReturnsManagedSerializer() {
        using var store = HashStore.Create().Create<int,int>(new ManagedTag(),new ManagedTag());
        Assert.IsType<ManagedDictionaryStore<int,int>>(store);
    }

    [Fact, Trait("Category","Unit")]
    public void Create_UnmanagedStruct_UnmanagedStruct_ReturnsUnmanagedDictionaryStore() {
        using var store = HashStore.Create().Create<UnmanagedStruct,UnmanagedStruct>();
        Assert.IsType<UnmanagedDictionaryStore<UnmanagedStruct,UnmanagedStruct>>(store);
    }

    [Fact, Trait("Category","Unit")]
    public void Create_ManagedStruct_ManagedStruct_ReturnsManagedSerializer() {
        using var store = HashStore.Create().Create<ManagedStruct,ManagedStruct>();
        Assert.IsType<ManagedDictionaryStore<ManagedStruct,ManagedStruct>>(store);
    }

    [Fact, Trait("Category","Unit")]
    public void Create_UnmanagedStruct_ManagedStruct_ReturnsUnmanagedAndManagedSerializers() {
        using var store = HashStore.Create().Create<UnmanagedStruct,ManagedStruct>();
        Assert.IsType<ManagedDictionaryStore<UnmanagedStruct,ManagedStruct>>(store);
    }

    [Fact, Trait("Category","Unit")]
    public void Create_ManagedStruct_UnmanagedStruct_ReturnsManagedAndUnmanagedSerializers() {
        using var store = HashStore.Create().Create<ManagedStruct,UnmanagedStruct>();
        Assert.IsType<ManagedDictionaryStore<ManagedStruct,UnmanagedStruct>>(store);
    }

    [Fact, Trait("Category","Unit")]
    public void CreateManagedManaged_ReturnsManagedManagedDictionary() {
        using var store = HashStore.Create().Create<string,string>();
        using var dictionary = store.Create();
        Assert.IsType<ManagedManagedDictionary<string,string,ManagedSerializer<string>,ManagedSerializer<string>>>(dictionary);
    }

    [Fact, Trait("Category","Unit")]
    public void CreateUnmanagedManaged_ReturnsUnmanagedManagedDictionary() {
        using var store = HashStore.Create().Create<int,string>();
        using var dictionary = store.Create();
        Assert.IsType<UnmanagedManagedDictionary<int,string,UnmanagedSerializer<int>,ManagedSerializer<string>>>(dictionary);
    }

    [Fact, Trait("Category","Unit")]
    public void CreateManagedUnmanaged_ReturnsManagedUnmanagedDictionary() {
        using var store = HashStore.Create().Create<string,int>();
        using var dictionary = store.Create();
        Assert.IsType<ManagedUnmanagedDictionary<string,int,ManagedSerializer<string>,UnmanagedSerializer<int>>>(dictionary);
    }

    [Fact, Trait("Category","Unit")]
    public void CreateUnmanagedUnmanaged_ReturnsUnmanagedUnmanagedDictionary() {
        using var store = HashStore.Create().Create<int,int>();
        using var dictionary = store.Create();
        Assert.IsType<UnmanagedUnmanagedDictionary<int,int,UnmanagedSerializer<int>,UnmanagedSerializer<int>>>(dictionary);
    }

    [Fact, Trait("Category","Unit")]
    public void CreateManagedUnmanagedHashSet_ReturnsManagedUnmanagedHashSet() {
        using var store = HashStore.Create().Create<string>(new ManagedTag());
        using var set = store.Create();
        Assert.IsType<ManagedHashSet<string>>(set);
    }

    [Fact, Trait("Category","Unit")]
    public void CreateUnmanagedUnmanagedHashSet_ReturnsUnmanagedUnmanagedHashSet() {
        using var store = HashStore.Create().Create<int>();
        using var set = store.Create();
        Assert.IsType<UnmanagedHashSet<int>>(set);
    }

    [Fact, Trait("Category","Unit")]
    public void Create_StorageLimitOptions_PassedToStorageStore() {
        using var logDevice = new global::Seamless.Devices.MemoryOnlyDevice(global::Seamless.Const.MinObjectLogCapacity);
        using var objectLogDevice = new global::Seamless.Devices.MemoryOnlyDevice(global::Seamless.Const.MinObjectLogCapacity);
        using var store = HashStore.Create(
            MemorySize: global::Seamless.Const.MinObjectLogCapacity,
            StorageLimitFraction: 0.5,
            StorageReserveBytes: 1024
        ).Create<int,int>(logDevice,objectLogDevice);

        Assert.Equal(global::Seamless.Const.MinObjectLogCapacity/2,store.StorageOverflowMutableStrategy.LimitBytes);
    }

    [Fact, Trait("Category","Unit")]
    public void Create_InvalidStorageLimitOptions_Throws() {
        Assert.Throws<ArgumentOutOfRangeException>(() => HashStore.Create(StorageLimitFraction: -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => HashStore.Create(StorageLimitFraction: 1.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => HashStore.Create(StorageLimitFraction: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => HashStore.Create(StorageReserveBytes: -1));
    }
}
