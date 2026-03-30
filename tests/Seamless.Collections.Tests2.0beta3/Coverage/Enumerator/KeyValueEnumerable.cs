using System.Collections;
using SF    = global::Seamless.SessionFunctions.Add;
using Store = global::Tsavorite.core.StoreFunctions<global::Tsavorite.core.SpanByteComparer, global::Tsavorite.core.DefaultRecordDisposer>;
using Alloc = global::Tsavorite.core.ObjectAllocator<global::Tsavorite.core.StoreFunctions<global::Tsavorite.core.SpanByteComparer, global::Tsavorite.core.DefaultRecordDisposer>>;
namespace Seamless.Collections.Tests;

public partial class KeyValueEnumerable {
    [Fact, Trait("Category","Coverage")]
    public void GetEnumerator_IEnumerableT() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        var kve = new global::Seamless.Collections.KeyValueEnumerable<int, string, SF, Store, Alloc>(dict.ClientSession);
        IEnumerable<KeyValuePair<int,string>> ie = kve;
        Assert.Equal(1,ie.Count());
    }
    [Fact, Trait("Category","Coverage")]
    public void GetEnumerator_IEnumerable() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        var kve = new global::Seamless.Collections.KeyValueEnumerable<int, string, SF, Store, Alloc>(dict.ClientSession);
        IEnumerable ie = kve;
        var en = ie.GetEnumerator();
        var count = 0;
        while(en.MoveNext()) count++;
        Assert.Equal(1,count);
    }
}
