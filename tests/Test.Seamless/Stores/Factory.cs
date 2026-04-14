using Seamless.Collections;
using Seamless.Stores;
namespace Test.Seamless.Collections;

public class FactoryTests
{
    // ── Memory Dictionary ──────────────────────────────────────────
    [Fact, Trait("Category", "Unit")]
    public void Create_UnmanagedKey_UnmanagedValue_UnmanagedDictionaryStoreを返す()
    {
        using var store = Factory.Create().Create<int, long>();
        Assert.IsType<UnmanagedDictionaryStore<int, long>>(store);
    }

    [Fact, Trait("Category", "Unit")]
    public void Create_UnmanagedKey_ManagedValue_UnmanagedSerializerとManagedSerializerを返す()
    {
        using var store = Factory.Create().Create<int, string>();
        Assert.IsType<ManagedDictionaryStore<int, string, UnmanagedSerializer<int>, ManagedSerializer<string>>>(store);
    }

    [Fact, Trait("Category", "Unit")]
    public void Create_ManagedKey_UnmanagedValue_ManagedSerializerとUnmanagedSerializerを返す()
    {
        using var store = Factory.Create().Create<string, int>();
        Assert.IsType<ManagedDictionaryStore<string, int, ManagedSerializer<string>, UnmanagedSerializer<int>>>(store);
    }

    [Fact, Trait("Category", "Unit")]
    public void Create_ManagedKey_ManagedValue_ManagedSerializerを返す()
    {
        using var store = Factory.Create().Create<string, string>();
        Assert.IsType<ManagedDictionaryStore<string, string, ManagedSerializer<string>, ManagedSerializer<string>>>(store);
    }

    // ── ManagedTag 明示指定 ────────────────────────────────────────
    [Fact, Trait("Category", "Unit")]
    public void Create_ManagedTag明示_UnmanagedKey_UnmanagedValue_ManagedSerializerを返す()
    {
        using var store = Factory.Create().Create<int, int>(new ManagedTag(), new ManagedTag());
        Assert.IsType<ManagedDictionaryStore<int, int, ManagedSerializer<int>, ManagedSerializer<int>>>(store);
    }

    // ── カスタム構造体 ─────────────────────────────────────────────
    [Fact, Trait("Category", "Unit")]
    public void Create_UnmanagedStruct_UnmanagedStruct_UnmanagedDictionaryStoreを返す()
    {
        using var store = Factory.Create().Create<UnmanagedStruct, UnmanagedStruct>();
        Assert.IsType<UnmanagedDictionaryStore<UnmanagedStruct, UnmanagedStruct>>(store);
    }

    [Fact, Trait("Category", "Unit")]
    public void Create_ManagedStruct_ManagedStruct_ManagedSerializerを返す()
    {
        using var store = Factory.Create().Create<ManagedStruct, ManagedStruct>();
        Assert.IsType<ManagedDictionaryStore<ManagedStruct, ManagedStruct, ManagedSerializer<ManagedStruct>, ManagedSerializer<ManagedStruct>>>(store);
    }

    [Fact, Trait("Category", "Unit")]
    public void Create_UnmanagedStruct_ManagedStruct_UnmanagedSerializerとManagedSerializerを返す()
    {
        using var store = Factory.Create().Create<UnmanagedStruct, ManagedStruct>();
        Assert.IsType<ManagedDictionaryStore<UnmanagedStruct, ManagedStruct, UnmanagedSerializer<UnmanagedStruct>, ManagedSerializer<ManagedStruct>>>(store);
    }

    [Fact, Trait("Category", "Unit")]
    public void Create_ManagedStruct_UnmanagedStruct_ManagedSerializerとUnmanagedSerializerを返す()
    {
        using var store = Factory.Create().Create<ManagedStruct, UnmanagedStruct>();
        Assert.IsType<ManagedDictionaryStore<ManagedStruct, UnmanagedStruct, ManagedSerializer<ManagedStruct>, UnmanagedSerializer<UnmanagedStruct>>>(store);
    }
}
