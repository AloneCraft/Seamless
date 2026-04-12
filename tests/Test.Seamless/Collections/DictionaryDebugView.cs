using Seamless.Collections;
using Seamless.Stores;
namespace Test.Seamless.Collections;

public class DictionaryDebugViewTests {
    [Fact, Trait("Category","Unit")]
    public void Items_全エントリを返す() {
        using var store = Factory.Create().Create<int,string>();
        using var dict = store.Create();
        dict.Add(2,"b");
        dict.Add(1,"a");
        var view = new DebugView<int,string>(dict);
        var items = view.Items;
        Assert.Equal(2,items.Length);
        Assert.Contains(items,x => x.Key==1&&x.Value=="a");
        Assert.Contains(items,x => x.Key==2&&x.Value=="b");
    }
    [Fact, Trait("Category","Unit")]
    public void ctor_null_例外() {
        Assert.Throws<ArgumentNullException>(() => new DebugView<int,string>(null!));
    }
}
