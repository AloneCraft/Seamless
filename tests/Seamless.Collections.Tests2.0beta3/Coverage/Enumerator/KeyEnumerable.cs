using System.Collections;
using SF    = global::Seamless.SessionFunctions.Add;
using Store = global::Tsavorite.core.StoreFunctions<global::Tsavorite.core.SpanByteComparer, global::Tsavorite.core.DefaultRecordDisposer>;
using Alloc = global::Tsavorite.core.ObjectAllocator<global::Tsavorite.core.StoreFunctions<global::Tsavorite.core.SpanByteComparer, global::Tsavorite.core.DefaultRecordDisposer>>;
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
