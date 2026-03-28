using Xunit;
namespace Seamless.Collections.Tests;

[Collection("Sequential")]
public class Collection {
    [Fact, Trait("Category","Coverage")]
    public void IsDisposed_Dictionary_Dispose前後() {
        var store = new DictionaryStore<int,string>();
        var dict = store.Create();
        Assert.False(dict.IsDisposed);
        dict.Dispose();
        Assert.True(dict.IsDisposed);
        store.Dispose();
    }
    [Fact, Trait("Category","Coverage")]
    public void IsDisposed_HashSet_Dispose前後() {
        var store = new HashSetStore<int>();
        var set = store.Create();
        Assert.False(set.IsDisposed);
        set.Dispose();
        Assert.True(set.IsDisposed);
        store.Dispose();
    }
    [Fact, Trait("Category","Coverage")]
    public void Dispose_Dictionary_二重呼び出し_冪等() {
        var store = new DictionaryStore<int,string>();
        var dict = store.Create();
        dict.Dispose();
        dict.Dispose();
        Assert.True(dict.IsDisposed);
        store.Dispose();
    }
    [Fact, Trait("Category","Coverage")]
    public void Dispose_HashSet_二重呼び出し_冪等() {
        var store = new HashSetStore<int>();
        var set = store.Create();
        set.Dispose();
        set.Dispose();
        Assert.True(set.IsDisposed);
        store.Dispose();
    }
    [Fact, Trait("Category","Coverage")]
    public void Dispose_TestCollection_二重呼び出し_Collection5早期リターン() {
        var store = new HashSetStore<int>();
        var subject = new TestCollection<int,Seamless.Collections.Empty>(store);
        subject.Dispose();
        subject.Dispose();
        Assert.True(subject.IsDisposed);
        store.Dispose();
    }
}
