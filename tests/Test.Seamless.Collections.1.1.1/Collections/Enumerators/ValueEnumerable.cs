using System.Collections;
using Store = Tsavorite.core.StoreFunctions<Tsavorite.core.SpanByte, Tsavorite.core.SpanByte, Tsavorite.core.SpanByteComparer, Tsavorite.core.SpanByteRecordDisposer>;

using GAllocator = Tsavorite.core.SpanByteAllocator<Tsavorite.core.StoreFunctions<Tsavorite.core.SpanByte,Tsavorite.core.SpanByte,Tsavorite.core.SpanByteComparer,Tsavorite.core.SpanByteRecordDisposer>>;
using GStoreFunctions = Tsavorite.core.StoreFunctions<Tsavorite.core.SpanByte,Tsavorite.core.SpanByte,Tsavorite.core.SpanByteComparer,Tsavorite.core.SpanByteRecordDisposer>;
namespace Seamless.Collections.Tests;

public class ValueEnumerable {
    [Fact, Trait("Category","Coverage")]
    public void GetEnumerator() {
        using var store = new DictionaryStore<int,string>();
        
using GAllocator = Tsavorite.core.SpanByteAllocator<Tsavorite.core.StoreFunctions<Tsavorite.core.SpanByte,Tsavorite.core.SpanByte,Tsavorite.core.SpanByteComparer,Tsavorite.core.SpanByteRecordDisposer>>;
using GStoreFunctions = Tsavorite.core.StoreFunctions<Tsavorite.core.SpanByte,Tsavorite.core.SpanByte,Tsavorite.core.SpanByteComparer,Tsavorite.core.SpanByteRecordDisposer>;
        dict.Add(1,"a");
        var ve = new Enumerators.ValueEnumerable<int, string, Seamless.SessionFunctions.Add, Store, Alloc>(dict.ClientSession);
        var count = 0;
        foreach(var _ in ve) count++;
        Assert.Equal(1,count);
    }
    [Fact, Trait("Category","Coverage")]
    public void GetEnumerator_IEnumerable() {
        using var store = new DictionaryStore<int,string>();
        
using GAllocator = Tsavorite.core.SpanByteAllocator<Tsavorite.core.StoreFunctions<Tsavorite.core.SpanByte,Tsavorite.core.SpanByte,Tsavorite.core.SpanByteComparer,Tsavorite.core.SpanByteRecordDisposer>>;
using GStoreFunctions = Tsavorite.core.StoreFunctions<Tsavorite.core.SpanByte,Tsavorite.core.SpanByte,Tsavorite.core.SpanByteComparer,Tsavorite.core.SpanByteRecordDisposer>;
        dict.Add(1,"a");
        var ve = new Enumerators.ValueEnumerable<int, string, Seamless.SessionFunctions.Add, Store, Alloc>(dict.ClientSession);
        IEnumerable ie = ve;
        var en = ie.GetEnumerator();
        var count = 0;
        while(en.MoveNext()) count++;
        Assert.Equal(1,count);
    }
}

