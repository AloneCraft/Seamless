using Xunit;
namespace Seamless.Collections.Tests;

[Collection("Sequential")]
public class CollectionBase {
    [Fact, Trait("Category","Coverage")]
    public void EntryCount_追加後に増加する() {
        using var store = new HashSetStore<int>();
        using var writer = store.Create();
        using var subject = new TestCollection<int,Seamless.Collections.Empty>(store);
        writer.Add(1);
        Assert.True(subject.EntryCount>=1);
    }
    [Fact, Trait("Category","Coverage")]
    public void GetLongCount_空の場合ゼロを返す() {
        using var store = new HashSetStore<int>();
        using var subject = new TestCollection<int,Seamless.Collections.Empty>(store);
        Assert.Equal(0L,subject.GetLongCount());
    }
    [Fact, Trait("Category","Coverage")]
    public void Count_GetLongCountと一致する() {
        using var store = new HashSetStore<int>();
        using var writer = store.Create();
        using var subject = new TestCollection<int,Seamless.Collections.Empty>(store);
        writer.Add(1);
        writer.Add(2);
        Assert.Equal((int)subject.GetLongCount(),subject.Count);
    }
    [Fact, Trait("Category","Coverage")]
    public void Contains_存在するキー_真を返す() {
        using var store = new HashSetStore<int>();
        using var writer = store.Create();
        using var subject = new TestCollection<int,Seamless.Collections.Empty>(store);
        writer.Add(1);
        Assert.True(subject.Contains(1));
    }
    [Fact, Trait("Category","Coverage")]
    public void Contains_存在しないキー_偽を返す() {
        using var store = new HashSetStore<int>();
        using var subject = new TestCollection<int,Seamless.Collections.Empty>(store);
        Assert.False(subject.Contains(99));
    }
    [Fact, Trait("Category","Coverage")]
    public void Remove_存在するキー_真を返す() {
        using var store = new HashSetStore<int>();
        using var writer = store.Create();
        using var subject = new TestCollection<int,Seamless.Collections.Empty>(store);
        writer.Add(1);
        Assert.True(subject.Remove(1));
    }
    [Fact, Trait("Category","Coverage")]
    public void Remove_存在しないキー_偽を返す() {
        using var store = new HashSetStore<int>();
        using var subject = new TestCollection<int,Seamless.Collections.Empty>(store);
        Assert.False(subject.Remove(99));
    }
}
