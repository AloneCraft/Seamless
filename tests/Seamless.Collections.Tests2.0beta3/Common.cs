namespace Seamless.Collections;

using Garnet.common;

using MemoryPack;
using Tsavorite.core;
internal static class Common {
    public static void Upsert<TSessionFunctions, TStoreFunctions, TAllocator>(ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,TSessionFunctions,TStoreFunctions,TAllocator> Session,int count0)
        where TSessionFunctions : ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>
        where TStoreFunctions : IStoreFunctions
        where TAllocator : IAllocator<TStoreFunctions> {
        for(var Key = 0; Key<count0; Key++) {
            var KeyBytes = MemoryPackSerializer.Serialize(Key);
            var ValueBytes = MemoryPackSerializer.Serialize(Key);
            Session.BasicContext.Upsert((FixedSpanByteKey)KeyBytes,ValueBytes);
        }
        CompletePendingWithOutputs(Session);
    }
    public static void CompletePendingWithOutputs<TSessionFunctions, TStoreFunctions, TAllocator>(ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,TSessionFunctions,TStoreFunctions,TAllocator> Session)
        where TSessionFunctions : ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>
        where TStoreFunctions : IStoreFunctions
        where TAllocator : IAllocator<TStoreFunctions> {
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
    public static void Delete<TSessionFunctions, TStoreFunctions, TAllocator>(ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,TSessionFunctions,TStoreFunctions,TAllocator> Session,int start,int end)
        where TSessionFunctions : ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>
        where TStoreFunctions : IStoreFunctions
        where TAllocator : IAllocator<TStoreFunctions> {
        for(var Key = start; Key<end; Key++) {
            var KeyBytes = MemoryPackSerializer.Serialize(Key);
            var Status = Session.BasicContext.Delete((FixedSpanByteKey)KeyBytes);
        }
        CompletePendingWithOutputs(Session);
    }
    public static ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,SessionFunctions,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,ObjectAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>> CreateSession(TsavoriteKV<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,ObjectAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>> Store) {
        return Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,SessionFunctions>(new SessionFunctions());
    }
    public static ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,SpanByteFunctions<Empty>,StoreFunctions<SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,SpanByteRecordDisposer>>> CreateSession(TsavoriteKV<StoreFunctions<SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,SpanByteRecordDisposer>>> Store) {
        return Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,SpanByteFunctions<Empty>>(new());
    }

    // Generic overload above covers other ClientSession specializations

    //public static void Count<TSessionFunctions, TStoreFunctions, TAllocator>(ITestOutputHelper output,ClientSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Empty,TSessionFunctions,TStoreFunctions,TAllocator> Session,IEnumerable<int> expected,string label)
    //    where TSessionFunctions : ISessionFunctions<PinnedSpanByte,SpanByteAndMemory,Empty>
    //    where TStoreFunctions : IStoreFunctions
    //    where TAllocator : IAllocator<TStoreFunctions> {
    //    var Keys = new List<int>();
    //    var Iterator = new KeyValueIterator<int,int>();
    //    var Chunk = Iterator.Chunk;
    //    var Chunk_Length = Chunk.Length;
    //    var Counts = new List<int>();
    //    long Cursor = 0;
    //    while(true) {
    //        var r = Session.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
    //        Assert.False(r);
    //        var Count = Iterator.Count;
    //        Counts.Add(Count);
    //        for(var a = 0; a<Count; a++) {
    //            var KeyValue = Iterator.Chunk[a];
    //            Keys.Add(KeyValue.Key);
    //        }
    //        if(Count<Chunk_Length) break;
    //    }
    //    //Keys.Sort();
    //    var A = Keys;
    //    var B = expected.ToList();
    //    var allKeys = A.Select(x => x).Union(B.Select(x => x));
    //    var fullOuterJoin = from key in allKeys
    //                        join a in A on key equals a into tempA
    //                        from a in tempA.DefaultIfEmpty()
    //                        join b in B on key equals b into tempB
    //                        from b in tempB.DefaultIfEmpty()
    //                        orderby key
    //                        select new { actual = a,expected = b };
    //    output.WriteLine(label);
    //    var c = true;
    //    if(A.Count!=B.Count) {
    //        foreach(var a in fullOuterJoin) {
    //            if(a.actual!=a.expected) {
    //                c=false;
    //            }
    //            output.WriteLine(a.ToString());
    //        }
    //    }
    //    Assert.True(c);
    //}
}
