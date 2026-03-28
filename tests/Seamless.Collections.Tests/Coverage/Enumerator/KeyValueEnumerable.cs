using Xunit;
namespace Seamless.Collections.Tests;

[Collection("Sequential")]
public partial class KeyValueEnumerable {
    [Fact, Trait("Category","Coverage")]
    public void GetEnumerator() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(10,"ten");
        var count = 0;
        foreach(var _ in dict) count++;
        Assert.Equal(1,count);
    }
}
