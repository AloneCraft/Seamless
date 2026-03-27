using Xunit;
using Xunit.Abstractions;
namespace Seamless.Collections;

using MemoryPack;
using Tsavorite.core;
internal static class Common {
    public static void Upsert<TSessionFunctions, TStoreFunctions, TAllocator>(ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty,TSessionFunctions,TStoreFunctions,TAllocator> Session,int count0)
        where TSessionFunctions : ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty>
        where TStoreFunctions : IStoreFunctions<SpanByte,SpanByte>
        where TAllocator : IAllocator<SpanByte,SpanByte,TStoreFunctions> {
        for(var Key = 0; Key<count0; Key++) {
            var KeyBytes = MemoryPackSerializer.Serialize(Key);
            var ValueBytes = MemoryPackSerializer.Serialize(Key);
            unsafe {
                fixed(byte* pKey = KeyBytes)
                fixed(byte* pValue = ValueBytes) {
                    var keySpan = SpanByte.FromPinnedPointer(pKey,KeyBytes.Length);
                    var valueSpan = SpanByte.FromPinnedPointer(pValue,ValueBytes.Length);
                    var status = Session.BasicContext.Upsert(ref keySpan,ref valueSpan);
                    if(status.IsPending) {
                        Session.BasicContext.CompletePendingWithOutputs(out var completedOutputs,wait: true);
                        completedOutputs.Next();
                        status=completedOutputs.Current.Status;
                        completedOutputs.Dispose();
                    }
                }
            }
        }
        CompletePendingWithOutputs(Session);
    }
    public static void CompletePendingWithOutputs<TSessionFunctions, TStoreFunctions, TAllocator>(ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty,TSessionFunctions,TStoreFunctions,TAllocator> Session)
        where TSessionFunctions : ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty>
        where TStoreFunctions : IStoreFunctions<SpanByte,SpanByte>
        where TAllocator : IAllocator<SpanByte,SpanByte,TStoreFunctions> {
        Session.BasicContext.CompletePendingWithOutputs(out var outputs,wait: true);
        using var completedOutputs = outputs;
        while(completedOutputs.Next()) {
            var status = completedOutputs.Current.Status;
            if(status.IsFaulted) {
                /* エラー処理 */
            }
            // completedOutputs.Current.Output で結果取得
        }
    }
    public static void Delete<TSessionFunctions, TStoreFunctions, TAllocator>(ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty,TSessionFunctions,TStoreFunctions,TAllocator> Session,int start,int end)
        where TSessionFunctions : ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty>
        where TStoreFunctions : IStoreFunctions<SpanByte,SpanByte>
        where TAllocator : IAllocator<SpanByte,SpanByte,TStoreFunctions> {
        for(var Key = start; Key<end; Key++) {
            var KeyBytes = MemoryPackSerializer.Serialize(Key);
            unsafe {
                fixed(byte* pKey = KeyBytes) {
                    var keySpan = SpanByte.FromPinnedPointer(pKey,KeyBytes.Length);
                    var Status = Session.BasicContext.Delete(ref keySpan);
                    if(Status.IsPending) {
                        Session.BasicContext.CompletePendingWithOutputs(out var completedOutputs,wait: true);
                        completedOutputs.Next();
                        Status=completedOutputs.Current.Status;
                        completedOutputs.Dispose();
                    }
                }
            }
        }
        CompletePendingWithOutputs(Session);
    }
    public static ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty,SessionFunctions,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>> CreateSession(TsavoriteKV<SpanByte,SpanByte,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>> Store) {
        return Store.NewSession<SpanByte,SpanByteAndMemory,Empty,SessionFunctions>(new SessionFunctions());
    }

    // Generic overload above covers other ClientSession specializations

    public static void Count<TSessionFunctions, TStoreFunctions, TAllocator>(ITestOutputHelper output,ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty,TSessionFunctions,TStoreFunctions,TAllocator> Session,IEnumerable<int> expected,string label)
        where TSessionFunctions : ISessionFunctions<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty>
        where TStoreFunctions : IStoreFunctions<SpanByte,SpanByte>
        where TAllocator : IAllocator<SpanByte,SpanByte,TStoreFunctions> {
        var Keys = new List<int>();
        var Iterator = new KeyValueIterator<int,int>();
        var Counts = new List<int>();
        {
            long Cursor = 0;
            while(true) {
                var r = Session.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
                Assert.False(r);
                var Count = Iterator.Count;
                Counts.Add(Count);
                for(var a = 0; a<Count; a++) {
                    var KeyValue = Iterator.Chunk[a];
                    Keys.Add(KeyValue.Key);
                }
                if(Count<Iterator.Chunk.Length) break;
            }
        }
        var A = Keys;
        var B = expected.ToList();
        var allKeys = A.Select(x => x).Union(B.Select(x => x));
        var fullOuterJoin = from key in allKeys
                            join a in A on key equals a into tempA
                            from a in tempA.DefaultIfEmpty()
                            join b in B on key equals b into tempB
                            from b in tempB.DefaultIfEmpty()
                            orderby key
                            select new { actual = a,expected = b };
        output.WriteLine(label);
        var c = true;
        if(A.Count!=B.Count) {
            foreach(var a in fullOuterJoin) {
                if(a.actual!=a.expected) {
                    c=false;
                }
                output.WriteLine(a.ToString());
            }
        }
        Assert.True(c);
    }
}
