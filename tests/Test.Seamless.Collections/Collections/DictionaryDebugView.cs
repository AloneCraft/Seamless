using Seamless.Collections;
namespace Test.Seamless.Collections;

public class DictionaryDebugViewTests {
    [Fact, Trait("Category",Unit)]
    public void Items_全エントリを返す() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict.Add(2,"b");
        dict.Add(1,"a");
        var view = new global::Seamless.Collections.DictionaryDebugView<int,string,ManagedSerializer<int>,ManagedSerializer<string>>(dict);
        var items = view.Items;
        Assert.Equal(2,items.Length);
        Assert.Contains(items,x => x.Key==1&&x.Value=="a");
        Assert.Contains(items,x => x.Key==2&&x.Value=="b");
    }
    [Fact, Trait("Category",Unit)]
    public void ctor_null_例外() {
        Assert.Throws<ArgumentNullException>(() => new global::Seamless.Collections.DictionaryDebugView<int,string,ManagedSerializer<int>,ManagedSerializer<string>>(null!));
    }
}
