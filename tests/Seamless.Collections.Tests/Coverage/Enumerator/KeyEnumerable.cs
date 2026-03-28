using Xunit;
namespace Seamless.Collections.Tests;

[Collection("Sequential")]
public partial class KeyEnumerable {
    [Fact, Trait("Category","Coverage")]
    public void GetEnumerator() {
        using var store = new HashSetStore<int>();
        using var set = store.Create();
        set.Add(1);
        set.Add(2);
        var count = 0;
        foreach(var _ in set) count++;
        Assert.Equal(2,count);
    }
    [Fact, Trait("Category","Coverage")]
    public void GetEnumerator_IEnumerableT() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        var keys = dict.Keys;
        Assert.Equal(2,keys.Count);
        Assert.Contains(1,keys);
        Assert.Contains(2,keys);
    }
}
