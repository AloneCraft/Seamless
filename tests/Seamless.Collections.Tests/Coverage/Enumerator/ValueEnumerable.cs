using Xunit;
namespace Seamless.Collections.Tests;


public partial class ValueEnumerable {
    [Fact, Trait("Category","Coverage")]
    public void GetEnumerator_IEnumerableT() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        var values = dict.Values;
        Assert.Equal(2,values.Count);
        Assert.Contains("a",values);
        Assert.Contains("b",values);
    }
}
