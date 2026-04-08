namespace Seamless.StorageCollections.Test;

/// <summary>
/// Tests for the <see cref="IDictionary{TKey,TValue}"/> indexer (get and set).
/// </summary>
public abstract class IndexerTests<TStrategy,TKey,TValue>
    :DictionaryTestBase<TStrategy,TKey,TValue>
    where TStrategy:IDictTestStrategy<TKey,TValue>,new()
    where TKey:notnull{
    [Fact]
    public void Indexer_Get_ExistingKey_ReturnsValue(){
        var dict=CreateMemory();
        dict.Add(Key(),Value());
        Assert.Equal(Value(),dict[Key()]);
    }
    [Fact]
    public void Indexer_Get_MissingKey_ThrowsKeyNotFoundException(){
        var dict=CreateMemory();
        Assert.Throws<KeyNotFoundException>(()=>dict[Key()]);
    }
    [Fact]
    public void Indexer_Set_NewKey_CountIsOne(){
        var dict=CreateMemory();
        dict[Key()]=Value();
        Assert.Single(dict);
    }
    [Fact]
    public void Indexer_Set_ExistingKey_OverwritesValue(){
        var dict=CreateMemory();
        dict[Key()]=Value(0);
        dict[Key()]=Value(1);
        Assert.Single(dict);
        Assert.Equal(Value(1),dict[Key()]);
    }
    [Fact]
    public void Indexer_OnDisk_Get_ExistingKey_ReturnsValue(){
        var dict=CreateDisk();
        dict.Add(Key(),Value());
        Assert.Equal(Value(),dict[Key()]);
    }
}

public sealed class IndexerTests_Managed:IndexerTests<ManagedStrategy,string,string>{}
public sealed class IndexerTests_Unmanaged:IndexerTests<UnmanagedStrategy,int,int>{}
