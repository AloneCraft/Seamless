using System.Collections;
using Seamless;
using Seamless.Collections;
using Seamless.Stores;
using Tsavorite.core;
namespace Test.Seamless.Unit.Collections;


public class DebugView {
    [Fact, Trait("Category","Unit")]public void UnmanagedDictionary() {
        using var store = Factory.Create().Create<int,int>();
        using var dict = store.Create();
        for(var a=0;a<10;a++){
            dict.Add(a,a);
        }
    }
    [Fact, Trait("Category","Unit")]public void UnmanagedHashSet() {
        using var store = Factory.Create().Create<int>();
        using var dict = store.Create();
        for(var a=0;a<10;a++){
            dict.Add(a);
        }
    }
    [Fact, Trait("Category","Unit")]public void ManagedDictionary() {
        using var store = Factory.Create().Create<string,string>();
        using var dict = store.Create();
        for(var a=0;a<10;a++){
            dict.Add(a.ToString(),a.ToString());
        }
    }
    [Fact, Trait("Category","Unit")]public void ManagedHashSet() {
        using var store = Factory.Create().Create<string>();
        using var dict = store.Create();
        for(var a=0;a<10;a++){
            dict.Add(a.ToString());
        }
    }
}
