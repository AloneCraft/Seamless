namespace Seamless.StorageCollections.Test;

/// <summary>
/// Tests for <see cref="IDictionary{TKey,TValue}.Remove(TKey)"/> and
/// <see cref="ICollection{T}.Remove(T)"/>.
/// </summary>
public abstract class RemoveTests<TStrategy,TKey,TValue>
    :DictionaryTestBase<TStrategy,TKey,TValue>
    where TStrategy:IDictTestStrategy<TKey,TValue>,new()
    where TKey:notnull{
    [Fact]
    public void Remove_ExistingKey_ReturnsTrue(){
        var dict=CreateMemory();
        dict.Add(Key(),Value());
        Assert.True(dict.Remove(Key()));
    }
    [Fact]
    public void Remove_ExistingKey_CountDecreases(){
        var dict=CreateMemory();
        dict.Add(Key(),Value());
        dict.Remove(Key());
        Assert.Empty(dict);
    }
    [Fact]
    public void Remove_MissingKey_ReturnsFalse(){
        var dict=CreateMemory();
        Assert.False(dict.Remove(Key()));
    }
    [Fact]
    public void Remove_KeyValuePair_ExistingPair_ReturnsTrue(){
        var dict=CreateMemory();
        dict.Add(Key(),Value());
        Assert.True(((ICollection<KeyValuePair<TKey,TValue>>)dict).Remove(new KeyValuePair<TKey,TValue>(Key(),Value())));
    }
    [Fact]
    public void Remove_OnDisk_ExistingKey_ReturnsTrue(){
        var dict=CreateDisk();
        dict.Add(Key(),Value());
        Assert.True(dict.Remove(Key()));
    }
}

public sealed class RemoveTests_Managed:RemoveTests<ManagedStrategy,string,string>{}
public sealed class RemoveTests_Unmanaged:RemoveTests<UnmanagedStrategy,int,int>{}
