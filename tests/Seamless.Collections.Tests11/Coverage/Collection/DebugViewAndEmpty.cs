using Xunit;
namespace Seamless.Collections.Tests;

[Collection("Sequential")]
public class DebugViewAndEmpty {
    [Fact, Trait("Category","Coverage")]
    public void Empty_EqualAndOperators() {
        var a = Empty.Default;
        var b = default(Empty);
        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object?)b));
        Assert.True(a==b);
        Assert.False(a!=b);
        Assert.Equal(0,a.GetHashCode());
    }
    [Fact, Trait("Category","Coverage")]
    public void DictionaryDebugView() {
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
    [Fact, Trait("Category","Coverage")]
    public void HashSetDebugView() {
        using var store = new HashSetStore<int>();
        using var set = store.Create();
        set.Add(10);
        set.Add(20);
        var view = new HashSetDebugView<int>(set);
        var items = view.Items;
        Assert.Equal(2,items.Length);
        Assert.Contains(10,items);
        Assert.Contains(20,items);
        Assert.Throws<ArgumentNullException>(() => new HashSetDebugView<int>(null!));
    }
}
