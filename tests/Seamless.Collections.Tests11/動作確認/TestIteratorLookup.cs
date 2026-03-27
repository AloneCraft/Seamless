using Common;
using MemoryPack;

using System.Diagnostics;

using Tsavorite.core;

using Xunit;
using Xunit.Abstractions;
namespace Seamless.Collections.Tests;

public class TestIteratorLookup(ITestOutputHelper output){
    private class KeyValueIterator<TKey,TValue>:IScanIteratorFunctions<SpanByte,SpanByte>{
        public readonly (TKey,TValue)[]Chunk=new (TKey,TValue)[Const.Chunk_Length];
        public int Count;
        public bool SingleReader(ref SpanByte key, ref SpanByte value, RecordMetadata recordMetadata, long numberOfRecords, out CursorRecordResult cursorRecordResult){
            Debug.Assert(!recordMetadata.RecordInfo.Tombstone);
            var Count=this.Count;
            var Key=MemoryPackSerializer.Deserialize<TKey>(key.AsReadOnlySpan())!;
            var Value=MemoryPackSerializer.Deserialize<TValue>(value.AsReadOnlySpan())!;
            Debug.Assert(Key!=null,nameof(Key)+" != null");
            Debug.Assert(Value!=null,nameof(Value)+" != null");
            this.Chunk[Count]=(Key,Value);
            Count++;
            this.Count=Count;
            if(Count==Const.Chunk_Length){
                cursorRecordResult=CursorRecordResult.EndBatch;
                return false;
            } else{
                cursorRecordResult=CursorRecordResult.Accept;
                return true;
            }
        }
        public bool OnStart(long beginAddress,long endAddress){
            this.Count=0;
            return true;
        }
        public void OnException(Exception exception,long numberOfRecords){}
        public void OnStop(bool completed,long numberOfRecords){
        }
        public bool ConcurrentReader(ref SpanByte key, ref SpanByte value, RecordMetadata recordMetadata, long numberOfRecords, out CursorRecordResult cursorRecordResult){
            return this.SingleReader(ref key, ref value, recordMetadata, numberOfRecords, out cursorRecordResult);
        }
    }
    [Fact]public void 走査0(){
        const int count0=253;
        using var LogDevice=new LocalMemoryDevice(capacity:Const.MinLogCapacity,sz_segment:Const.SegmentSize,parallelism:1,sector_size:64U,latencyMs:0,fileName:@$"{Environment.CurrentDirectory}\LogDevice.bin");
        var Settings=new KVSettings<SpanByte, SpanByte>{IndexSize=Const.IndexSize,MemorySize=Const.LogMemorySize,PageSize=Const.PageSize,SegmentSize=Const.SegmentSize,MutableFraction=1,LogDevice=LogDevice,};
        var StoreFunctions = new StoreFunctions<SpanByte, SpanByte, SpanByteComparer, SpanByteRecordDisposer>(new SpanByteComparer(),() => null,() => null,new SpanByteRecordDisposer());
        using var Store = new TsavoriteKV<SpanByte, SpanByte,StoreFunctions<SpanByte, SpanByte, SpanByteComparer, SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte, SpanByte, SpanByteComparer, SpanByteRecordDisposer>>>(Settings,StoreFunctions,static (AllocSettings, StoreFuncs) => new SpanByteAllocator<StoreFunctions<SpanByte, SpanByte, SpanByteComparer, SpanByteRecordDisposer>>(AllocSettings, StoreFuncs));
        using var Session=Common.CreateSession(Store);
        Common.Upsert(Session,count0);
        var List=new List<(int Key,int Value)>();
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
        Common.Count(output,Session,Enumerable.Range(0,count0),"Upsert後");
    }
}
