namespace Seamless.StorageCollections.Test;

/// <summary>
/// Tests for <see cref="IDictionary{TKey,TValue}.TryGetValue"/>.
/// </summary>
public abstract class TryGetValueTests<TStrategy,TKey,TValue>
    :DictionaryTestBase<TStrategy,TKey,TValue>
    where TStrategy:IDictTestStrategy<TKey,TValue>,new()
    where TKey:notnull{
    [Fact]
    public void TryGetValue_ExistingKey_ReturnsTrueAndCorrectValue(){
        var dict=CreateMemory();
        dict.Add(Key(),Value());
        Assert.True(dict.TryGetValue(Key(),out var actual));
        Assert.Equal(Value(),actual);
    }
    [Fact]
    public void TryGetValue_MissingKey_ReturnsFalse(){
        var dict=CreateMemory();
        Assert.False(dict.TryGetValue(Key(),out _));
    }
    [Fact]
    public void TryGetValue_MultipleItems_ReturnsCorrectValue(){
        var dict=CreateMemory();
        dict.Add(Key(0),Value(0));
        dict.Add(Key(1),Value(1));
        Assert.True(dict.TryGetValue(Key(1),out var actual));
        Assert.Equal(Value(1),actual);
    }
    [Fact]
    public void TryGetValue_OnDisk_ExistingKey_ReturnsTrueAndCorrectValue(){
        var dict=CreateDisk();
        dict.Add(Key(),Value());
        Assert.True(dict.TryGetValue(Key(),out var actual));
        Assert.Equal(Value(),actual);
    }
}

public sealed class TryGetValueTests_Managed:TryGetValueTests<ManagedStrategy,string,string>{}
public sealed class TryGetValueTests_Unmanaged:TryGetValueTests<UnmanagedStrategy,int,int>{}
