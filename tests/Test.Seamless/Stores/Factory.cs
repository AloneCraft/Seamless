using Seamless.Collections;
using Seamless.Stores;
namespace Test.Seamless.Collections;

public class FactoryTests {
    // ── Memory Dictionary ──────────────────────────────────────────
    [Fact, Trait("Category","Unit")]
    public void Create_UnmanagedKey_UnmanagedValue_UnmanagedDictionaryStoreを返す() {
        using var store = Factory.Create().Create<int,long>();
        Assert.IsType<UnmanagedUnmanagedDictionaryStore<int,long>>(store);
    }

    [Fact, Trait("Category","Unit")]
    public void Create_UnmanagedKey_ManagedValue_UnmanagedSerializerとManagedSerializerを返す() {
        using var store = Factory.Create().Create<int,string>();
        Assert.IsType<UnmanagedManagedDictionaryStore<int,string>>(store);
    }

    [Fact, Trait("Category","Unit")]
    public void Create_ManagedKey_UnmanagedValue_ManagedSerializerとUnmanagedSerializerを返す() {
        using var store = Factory.Create().Create<string,int>();
        Assert.IsType<ManagedUnmanagedDictionaryStore<string,int>>(store);
    }

    [Fact, Trait("Category","Unit")]
    public void Create_ManagedKey_ManagedValue_ManagedSerializerを返す() {
        using var store = Factory.Create().Create<string,string>();
        Assert.IsType<ManagedManagedDictionaryStore<string,string>>(store);
    }

    // ── ManagedTag 明示指定 ────────────────────────────────────────
    [Fact, Trait("Category","Unit")]
    public void Create_ManagedTag明示_UnmanagedKey_UnmanagedValue_ManagedSerializerを返す() {
        using var store = Factory.Create().Create<int,int>(new ManagedTag(),new ManagedTag());
        Assert.IsType<ManagedManagedDictionaryStore<int,int>>(store);
    }

    // ── カスタム構造体 ─────────────────────────────────────────────
    [Fact, Trait("Category","Unit")]
    public void Create_UnmanagedStruct_UnmanagedStruct_UnmanagedDictionaryStoreを返す() {
        using var store = Factory.Create().Create<UnmanagedStruct,UnmanagedStruct>();
        Assert.IsType<UnmanagedUnmanagedDictionaryStore<UnmanagedStruct,UnmanagedStruct>>(store);
    }

    [Fact, Trait("Category","Unit")]
    public void Create_ManagedStruct_ManagedStruct_ManagedSerializerを返す() {
        using var store = Factory.Create().Create<ManagedStruct,ManagedStruct>();
        Assert.IsType<ManagedManagedDictionaryStore<ManagedStruct,ManagedStruct>>(store);
    }

    [Fact, Trait("Category","Unit")]
    public void Create_UnmanagedStruct_ManagedStruct_UnmanagedSerializerとManagedSerializerを返す() {
        using var store = Factory.Create().Create<UnmanagedStruct,ManagedStruct>();
        Assert.IsType<UnmanagedManagedDictionaryStore<UnmanagedStruct,ManagedStruct>>(store);
    }

    [Fact, Trait("Category","Unit")]
    public void Create_ManagedStruct_UnmanagedStruct_ManagedSerializerとUnmanagedSerializerを返す() {
        using var store = Factory.Create().Create<ManagedStruct,UnmanagedStruct>();
        Assert.IsType<ManagedUnmanagedDictionaryStore<ManagedStruct,UnmanagedStruct>>(store);
    }

    [Fact, Trait("Category","Unit")]
    public void CreateManagedManaged_ManagedManagedDictionaryを返す() {
        using var store = Factory.Create().Create<string,string>();
        using var dictionary = store.Create();
        Assert.IsType<ManagedManagedDictionary<string,string,ManagedSerializer<string>,ManagedSerializer<string>>>(dictionary);
    }

    [Fact, Trait("Category","Unit")]
    public void CreateUnmanagedManaged_UnmanagedManagedDictionaryを返す() {
        using var store = Factory.Create().Create<int,string>();
        using var dictionary = store.Create();
        Assert.IsType<UnmanagedManagedDictionary<int,string,UnmanagedSerializer<int>,ManagedSerializer<string>>>(dictionary);
    }

    [Fact, Trait("Category","Unit")]
    public void CreateManagedUnmanaged_ManagedUnmanagedDictionaryを返す() {
        using var store = Factory.Create().Create<string,int>();
        using var dictionary = store.Create();
        Assert.IsType<ManagedUnmanagedDictionary<string,int,ManagedSerializer<string>,UnmanagedSerializer<int>>>(dictionary);
    }

    [Fact, Trait("Category","Unit")]
    public void CreateUnmanagedUnmanaged_UnmanagedUnmanagedDictionaryを返す() {
        using var store = Factory.Create().Create<int,int>();
        using var dictionary = store.Create();
        Assert.IsType<UnmanagedUnmanagedDictionary<int,int,UnmanagedSerializer<int>,UnmanagedSerializer<int>>>(dictionary);
    }

    [Fact, Trait("Category","Unit")]
    public void CreateManagedUnmanagedHashSet_ManagedUnmanagedHashSetを返す() {
        using var store = Factory.Create().Create<string>(new ManagedTag());
        using var set = store.Create();
        Assert.IsType<ManagedUnmanagedHashSet<string>>(set);
    }

    [Fact, Trait("Category","Unit")]
    public void CreateUnmanagedUnmanagedHashSet_UnmanagedUnmanagedHashSetを返す() {
        using var store = Factory.Create().Create<int>();
        using var set = store.Create();
        Assert.IsType<UnmanagedUnmanagedHashSet<int>>(set);
    }
}

