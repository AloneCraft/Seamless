namespace Seamless.StorageCollections.Test;

/// <summary>
/// Tests for <see cref="IDictionary{TKey,TValue}.Add(TKey,TValue)"/> and
/// <see cref="ICollection{T}.Add(T)"/>.
/// </summary>
public abstract class AddTests<TStrategy,TKey,TValue>
    :DictionaryTestBase<TStrategy,TKey,TValue>
    where TStrategy:IDictTestStrategy<TKey,TValue>,new()
    where TKey:notnull{
    [Fact]
    public void Add_SingleItem_CountIsOne(){
        var dict=CreateMemory();
        dict.Add(Key(),Value());
        Assert.Single(dict);
    }
    [Fact]
    public void Add_MultipleDistinctKeys_CountMatches(){
        var dict=CreateMemory();
        dict.Add(Key(0),Value(0));
        dict.Add(Key(1),Value(1));
        dict.Add(Key(2),Value(2));
        Assert.Equal(3,dict.Count);
    }
    [Fact]
    public void Add_DuplicateKey_ThrowsArgumentException(){
        var dict=CreateMemory();
        dict.Add(Key(),Value());
        Assert.Throws<ArgumentException>(()=>dict.Add(Key(),Value()));
    }
    [Fact]
    public void Add_KeyValuePair_CountIsOne(){
        var dict=CreateMemory();
        ((ICollection<KeyValuePair<TKey,TValue>>)dict).Add(new KeyValuePair<TKey,TValue>(Key(),Value()));
        Assert.Single(dict);
    }
    [Fact]
    public void Add_OnDisk_SingleItem_CountIsOne(){
        var dict=CreateDisk();
        dict.Add(Key(),Value());
        Assert.Single(dict);
    }
}

public sealed class AddTests_Managed:AddTests<ManagedStrategy,string,string>{}
public sealed class AddTests_Unmanaged:AddTests<UnmanagedStrategy,int,int>{}
