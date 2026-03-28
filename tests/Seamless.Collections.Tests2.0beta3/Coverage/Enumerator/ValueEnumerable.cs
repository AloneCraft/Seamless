using System.Collections;
using Xunit;
using SF    = global::Seamless.Collections.AddSessionFunctions;
using Store = global::Tsavorite.core.StoreFunctions<global::Tsavorite.core.SpanByteComparer, global::Tsavorite.core.DefaultRecordDisposer>;
using Alloc = global::Tsavorite.core.ObjectAllocator<global::Tsavorite.core.StoreFunctions<global::Tsavorite.core.SpanByteComparer, global::Tsavorite.core.DefaultRecordDisposer>>;
namespace Seamless.Collections.Tests;

public partial class ValueEnumerable {
    [Fact, Trait("Category","Coverage")]
    public void GetEnumerator() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        var ve = new global::Seamless.Collections.ValueEnumerable<int, string, SF, Store, Alloc>(dict.Session);
        var count = 0;
        foreach(var _ in ve) count++;
        Assert.Equal(1,count);
    }
    [Fact, Trait("Category","Coverage")]
    public void GetEnumerator_IEnumerable() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        var ve = new global::Seamless.Collections.ValueEnumerable<int, string, SF, Store, Alloc>(dict.Session);
        IEnumerable ie = ve;
        var en = ie.GetEnumerator();
        var count = 0;
        while(en.MoveNext()) count++;
        Assert.Equal(1,count);
    }
}
