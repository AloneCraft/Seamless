using Xunit;
namespace Seamless.Collections.Tests;

[Collection("Sequential")]
public class HashSetDebugView {
    [Fact, Trait("Category","Coverage")]
    public void Test() {
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
