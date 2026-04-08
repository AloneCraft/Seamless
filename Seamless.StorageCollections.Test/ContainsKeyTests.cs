namespace Seamless.StorageCollections.Test;

/// <summary>
/// Tests for <see cref="IDictionary{TKey,TValue}.ContainsKey"/>.
/// </summary>
public abstract class ContainsKeyTests<TStrategy,TKey,TValue>
    :DictionaryTestBase<TStrategy,TKey,TValue>
    where TStrategy:IDictTestStrategy<TKey,TValue>,new()
    where TKey:notnull{
    [Fact]
    public void ContainsKey_ExistingKey_ReturnsTrue(){
        var dict=CreateMemory();
        dict.Add(Key(),Value());
        Assert.True(dict.ContainsKey(Key()));
    }
    [Fact]
    public void ContainsKey_MissingKey_ReturnsFalse(){
        var dict=CreateMemory();
        Assert.False(dict.ContainsKey(Key()));
    }
    [Fact]
    public void ContainsKey_AfterRemove_ReturnsFalse(){
        var dict=CreateMemory();
        dict.Add(Key(),Value());
        dict.Remove(Key());
        Assert.False(dict.ContainsKey(Key()));
    }
    [Fact]
    public void ContainsKey_OnDisk_ExistingKey_ReturnsTrue(){
        var dict=CreateDisk();
        dict.Add(Key(),Value());
        Assert.True(dict.ContainsKey(Key()));
    }
}

public sealed class ContainsKeyTests_Managed:ContainsKeyTests<ManagedStrategy,string,string>{}
public sealed class ContainsKeyTests_Unmanaged:ContainsKeyTests<UnmanagedStrategy,int,int>{}
