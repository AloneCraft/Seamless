using Seamless.Collections;
using Seamless.Store;
namespace Test.Seamless.Collections;
public class HashSetDebugView {
    [Fact, Trait("Category",Unit)]
    public void Items_全エントリを返す() {
        using var store = Factory.Create().Create<int>();
        using var set = store.Create();
        set.Add(10);
        set.Add(20);
        var view = new HashSetDebugView<int>(set);
        var items = view.Items;
        Assert.Equal(2,items.Length);
        Assert.Contains(10,items);
        Assert.Contains(20,items);
    }
    [Fact, Trait("Category",Unit)]
    public void ctor_null_例外() {
        Assert.Throws<ArgumentNullException>(() => new HashSetDebugView<int>(null!));
    }
}
