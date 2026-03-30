using System.Collections;
using SF    = global::Seamless.Collections.AddSessionFunctions<int, global::Seamless.Collections.Empty>;
using Store = global::Tsavorite.core.StoreFunctions<global::Tsavorite.core.SpanByte, global::Tsavorite.core.SpanByte, global::Tsavorite.core.SpanByteComparer, global::Tsavorite.core.SpanByteRecordDisposer>;
using Alloc = global::Tsavorite.core.SpanByteAllocator<global::Tsavorite.core.StoreFunctions<global::Tsavorite.core.SpanByte, global::Tsavorite.core.SpanByte, global::Tsavorite.core.SpanByteComparer, global::Tsavorite.core.SpanByteRecordDisposer>>;
namespace Seamless.Collections.Tests;

public partial class KeyEnumerable {
    [Fact, Trait("Category","Coverage")]
    public void GetEnumerator_IEnumerable() {
        using var store = new HashSetStore<int>();
        using var set = store.Create();
        set.Add(1);
        var ke = new global::Seamless.Collections.KeyEnumerable<int, global::Seamless.Collections.Empty, SF, Store, Alloc>(set.ClientSession);
        IEnumerable ie = ke;
        var en = ie.GetEnumerator();
        var count = 0;
        while(en.MoveNext()) count++;
        Assert.Equal(1,count);
    }
}
