using Tsavorite.Stores;
namespace Test.Tsavorite.Collections;


public class DebugView {
    [Fact, Trait("Category","Unit")]public void UnmanagedDictionary() {
        using var store = Store.Create().Create<int,int>();
        using var dict = store.Create();
        for(var a=0;a<10;a++){
            dict.Add(a,a);
        }
    }
    [Fact, Trait("Category","Unit")]public void UnmanagedHashSet() {
        using var store = Store.Create().Create<int>();
        using var dict = store.Create();
        for(var a=0;a<10;a++){
            dict.Add(a);
        }
    }
    [Fact, Trait("Category","Unit")]public void Dictionary() {
        using var store = Store.Create().Create<string,string>();
        using var dict = store.Create();
        for(var a=0;a<10;a++){
            dict.Add(a.ToString(),a.ToString());
        }
    }
    [Fact, Trait("Category","Unit")]public void HashSet() {
        using var store = Store.Create().Create<string>();
        using var dict = store.Create();
        for(var a=0;a<10;a++){
            dict.Add(a.ToString());
        }
    }
}
