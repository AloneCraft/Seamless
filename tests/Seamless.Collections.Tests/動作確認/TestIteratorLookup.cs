using Common;
using Tsavorite.core;

using Xunit;
using Xunit.Abstractions;

namespace Seamless.Collections.Tests;

public class TestIteratorLookup(ITestOutputHelper output){
    [Fact]public void 走査0(){
        const int count0=254;
        using var LogDevice=new LocalMemoryDevice(capacity:Const.MinLogCapacity,sz_segment:Const.SegmentSize,parallelism:1,sector_size:64U,latencyMs:0,fileName:@$"{Environment.CurrentDirectory}\LogDevice.bin");
        var Settings=new KVSettings{IndexSize=Const.IndexSize,LogMemorySize=Const.LogMemorySize,PageSize=Const.PageSize,SegmentSize=Const.SegmentSize,MutableFraction=1,LogDevice=LogDevice,};
        var StoreFunctions=new StoreFunctions<SpanByteComparer,SpanByteRecordDisposer>(new SpanByteComparer(),()=>null,new SpanByteRecordDisposer());
        using var Store = new TsavoriteKV<StoreFunctions<SpanByteComparer, SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer, SpanByteRecordDisposer>>>(Settings,StoreFunctions,static (AllocSettings, StoreFuncs) => new(AllocSettings, StoreFuncs));
        using var Session=Common.CreateSession(Store);
        Common.Upsert(Session,count0);
        var List=new List<KeyValuePair<int,int>>();
        var Iterator=new KeyValueIterator<int,int>();
        var Chunk=Iterator.Chunk;
        var Chunk_Length=Chunk.Length;
        long Cursor=0;
        while(true){
            var r=Session.IterateLookup(ref Iterator,ref Cursor,resetCursor:false);
            Assert.False(r);
            var Count=Iterator.Count;
            List.AddRange(Chunk.AsSpan(0,Count));
            if(Count<Chunk_Length) break;
        }
    }
}
