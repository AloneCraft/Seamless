using Common;
using Tsavorite.core;
using Xunit;

namespace Seamless.Collections.Tests;

[Collection("Sequential")]
public class Collection5 {
    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize = Const.MinObjectLogCapacity;

    private sealed class ProbeCollection<TKey, TValue>:Collection<TKey,TValue,AddSessionFunctions<TKey,TValue>,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>> where TKey : IEquatable<TKey> where TValue : IEquatable<TValue> {
        public ProbeCollection(TsavoriteKV<SpanByte,SpanByte,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>> store,long logMemorySize,double mutableFraction) : base(store,new AddSessionFunctions<TKey,TValue>(),logMemorySize,mutableFraction) {
        }

        public void Upsert(TKey key,TValue value) {
            var keyBytes = Serialize(key);
            var valueBytes = Serialize(value);
            unsafe {
                fixed(byte* pKey = keyBytes, pValue = valueBytes) {
                    var fixedKey = SpanByte.FromPinnedPointer(pKey,keyBytes.Length);
                    var fixedValue = SpanByte.FromPinnedPointer(pValue,valueBytes.Length);
                    var status = this.Session.BasicContext.Upsert(fixedKey,fixedValue);
                    if(status.IsPending) {
                        this.Session.BasicContext.CompletePending(wait: true);
                    }
                }
            }
        }

        public bool CallWouldOverflowMutable(int keyLength,int valueLength) => this.WouldOverflowMutable(keyLength,valueLength);
        public void CallThrowLogFull() => this.ThrowLogFull();
    }

    [Fact, Trait("Category","Coverage")]
    public void Count_EntryCount_TryGetValue() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");

        Assert.Equal(2,dict.Count);
        Assert.True(dict.EntryCount>=2);

        Assert.True(dict.TryGetValue(1,out var value));
        Assert.Equal("a",value);
        Assert.False(dict.TryGetValue(999,out _));
    }

    [Fact, Trait("Category","Coverage")]
    public void Clear_And_Enumerables() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");

        var list = dict.ToList();
        Assert.Equal(2,list.Count);
        Assert.Equal(2,dict.Keys.Count);
        Assert.Equal(2,dict.Values.Count);
        Assert.Equal(2,dict.Count());

        dict.Clear();
        Assert.Equal(0,dict.Count);

        dict.Add(3,"c");
        Assert.True(dict.ContainsKey(3));
    }

    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void TryGetValue_IsPending() {
        var folder = CreateFolder();
        using var logDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var store = new DictionaryStore<int,string>(logDevice,objDevice,MemorySize: TestMemorySize,MutableFraction: 0);
        using var dict = store.Create();
        for(var a = 0; a<500; a++) {
            dict[a]=$"v{a}";
        }

        Assert.True(dict.TryGetValue(0,out var value));
        Assert.Equal("v0",value);
    }

    [Fact, Trait("Category","Coverage")]
    public void Dispose_MultipleTimes() {
        var store = new DictionaryStore<int,string>();
        var dict = store.Create();
        dict.Add(1,"a");

        dict.Dispose();
        dict.Dispose();
        store.Dispose();
        store.Dispose();
    }

    [Fact, Trait("Category","Coverage")]
    public void BaseRemove_Found_And_NotFound() {
        using var store = new DictionaryStore<int,string>();
        using var probe = new ProbeCollection<int,string>(store.Store,store.LogMemorySize,store.MutableFraction);
        probe.Upsert(10,"x");

        Assert.True(probe.Remove(10));
        Assert.False(probe.Remove(10));
    }

    [Fact, Trait("Category","Coverage")]
    public void WouldOverflowMutable_LogMemorySizeZero_And_ThrowLogFull() {
        using var store = new DictionaryStore<int,string>();
        using var probe = new ProbeCollection<int,string>(store.Store,0,1.0);

        Assert.False(probe.CallWouldOverflowMutable(1024,1024));
        Assert.Throws<OutOfMemoryException>(() => probe.CallThrowLogFull());
    }

    [Fact, Trait("Category","Coverage")]
    public void GetLongCount_Empty() {
        using var store = new DictionaryStore<int,string>();
        using var probe = new ProbeCollection<int,string>(store.Store,store.LogMemorySize,store.MutableFraction);

        Assert.Equal(0,probe.GetLongCount());
    }
}