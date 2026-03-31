using System.Collections;
using Store = Tsavorite.core.StoreFunctions<Tsavorite.core.SpanByte, Tsavorite.core.SpanByte, Tsavorite.core.SpanByteComparer, Tsavorite.core.SpanByteRecordDisposer>;
using Alloc = Tsavorite.core.SpanByteAllocator<Tsavorite.core.StoreFunctions<Tsavorite.core.SpanByte, Tsavorite.core.SpanByte, Tsavorite.core.SpanByteComparer, Tsavorite.core.SpanByteRecordDisposer>>;
namespace Seamless.Collections.Tests;

public class KeyEnumerable {
    [Fact, Trait("Category","Coverage")]
    public void GetEnumerator_IEnumerable() {
        using var store = new HashSetStore<int>();
        using var set = store.Create();
        set.Add(1);
        var ke = new Enumerators.KeyEnumerable<int, global::Seamless.Collections.Empty, Seamless.SessionFunctions.Add, Store, Alloc>(set.ClientSession);
        IEnumerable ie = ke;
        var en = ie.GetEnumerator();
        var count = 0;
        while(en.MoveNext()) count++;
        Assert.Equal(1,count);
    }
}
