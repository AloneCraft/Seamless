using Xunit;
namespace Seamless.Collections.Tests;

[Collection("Sequential")]
public class DictionaryDebugView {
    [Fact, Trait("Category","Coverage")]
    public void Test() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(2,"b");
        dict.Add(1,"a");
        var view = new DictionaryDebugView<int,string>(dict);
        var items = view.Items;
        Assert.Equal(2,items.Length);
        Assert.Contains(items,x => x.Key==1&&x.Value=="a");
        Assert.Contains(items,x => x.Key==2&&x.Value=="b");
        Assert.Throws<ArgumentNullException>(() => new DictionaryDebugView<int,string>(null!));
    }
}
