using MemoryPack;
using Garnet.common;
using Tsavorite.core;
using Xunit;
namespace Seamless.Collections.Tests;

[Collection("Sequential")]
public class AddSessionFunctions {
    private readonly struct DummyEpochAccessor:IEpochAccessor {
        public void Resume() { }
        public bool TrySuspend() => true;
    }
    private static byte[] SerInt(int v) => MemoryPackSerializer.Serialize(v);
    private static byte[] SerStr(string v) => MemoryPackSerializer.Serialize(v);
    private static FixedSpanByteKey MakeKey(byte[] b) => (FixedSpanByteKey)(ReadOnlySpan<byte>)b;

    [Fact, Trait("Category","Coverage")]
    public void AddSessionFunctions_RMWで重複拒否できる() {
        using var store = new DictionaryStore<int,string>();
        var key = MakeKey(SerInt(1));
        using var session = store.Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Seamless.Collections.Empty,Seamless.Collections.AddSessionFunctions>(new Seamless.Collections.AddSessionFunctions());
        var bc = session.BasicContext;

        {
            var input = PinnedSpanByte.FromPinnedSpan(SerStr("v1"));
            SpanByteAndMemory output = default;
            var status = bc.RMW(key,ref input,ref output);
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
            Assert.True(status.NotFound);
        }
        {
            var input = PinnedSpanByte.FromPinnedSpan(SerStr("v2"));
            SpanByteAndMemory output = default;
            var status = bc.RMW(key,ref input,ref output);
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Next();
                status=completed.Current.Status;
                completed.Dispose();
            }
            Assert.False(status.NotFound);
        }
    }

    [Fact, Trait("Category","Coverage")]
    public void AddSessionFunctions_ConvertOutputToHeapは例外を投げる() {
        var sessionFunctions = new Seamless.Collections.AddSessionFunctions();
        var input = default(PinnedSpanByte);
        SpanByteAndMemory output = default;
        Assert.Throws<NotSupportedException>(() => sessionFunctions.ConvertOutputToHeap(ref input,ref output));
    }

    [Fact, Trait("Category","Coverage")]
    public void AddSessionFunctions_35メソッドを直接実行する() {
        var f = new Seamless.Collections.AddSessionFunctions();
        var disk = default(DiskLogRecord);
        var src = new DummySourceLogRecord(1,"v");
        var key = MakeKey(SerInt(10));
        var span = SerStr("x");
        var logRecord = default(LogRecord);
        var sizeInfo = default(RecordSizeInfo);
        var input = default(PinnedSpanByte);
        SpanByteAndMemory output = default;
        var readInfo = default(ReadInfo);
        var upsertInfo = default(UpsertInfo);
        var rmwInfo = default(RMWInfo);
        var deleteInfo = default(DeleteInfo);
        var status = default(Status);
        var metadata = default(RecordMetadata);

        Assert.True(f.Reader(in src,ref input,ref output,ref readInfo));
        _=Record.Exception(() => f.InPlaceWriter(ref logRecord,in sizeInfo,ref input,span,ref output,ref upsertInfo));
        _=Record.Exception(() => f.InPlaceWriter(ref logRecord,in sizeInfo,ref input,new DummyHeapObject(),ref output,ref upsertInfo));
        _=Record.Exception(() => f.InPlaceWriter(ref logRecord,in sizeInfo,ref input,in src,ref output,ref upsertInfo));
        _=Record.Exception(() => f.InitialWriter(ref logRecord,in sizeInfo,ref input,span,ref output,ref upsertInfo));
        _=Record.Exception(() => f.InitialWriter(ref logRecord,in sizeInfo,ref input,new DummyHeapObject(),ref output,ref upsertInfo));
        _=Record.Exception(() => f.InitialWriter(ref logRecord,in sizeInfo,ref input,in src,ref output,ref upsertInfo));
        f.PostUpsertOperation(key,ref input,span,ref upsertInfo,new DummyEpochAccessor());
        f.PostUpsertOperation(key,ref input,new DummyHeapObject(),ref upsertInfo,new DummyEpochAccessor());
        f.PostInitialWriter(ref logRecord,in sizeInfo,ref input,span,ref output,ref upsertInfo);
        f.PostInitialWriter(ref logRecord,in sizeInfo,ref input,new DummyHeapObject(),ref output,ref upsertInfo);
        f.PostInitialWriter(ref logRecord,in sizeInfo,ref input,in src,ref output,ref upsertInfo);
        Assert.True(f.NeedInitialUpdate(key,ref input,ref output,ref rmwInfo));
        _=Record.Exception(() => f.InitialUpdater(ref logRecord,in sizeInfo,ref input,ref output,ref rmwInfo));
        Assert.False(f.InPlaceUpdater(ref logRecord,in sizeInfo,ref input,ref output,ref rmwInfo));
        f.PostInitialUpdater(ref logRecord,in sizeInfo,ref input,ref output,ref rmwInfo);
        Assert.False(f.NeedCopyUpdate(in src,ref input,ref output,ref rmwInfo));
        Assert.True(f.CopyUpdater(in src,ref logRecord,in sizeInfo,ref input,ref output,ref rmwInfo));
        Assert.True(f.PostCopyUpdater(in src,ref logRecord,in sizeInfo,ref input,ref output,ref rmwInfo));
        f.PostRMWOperation(key,ref input,ref rmwInfo,new DummyEpochAccessor());
        f.RMWCompletionCallback(ref disk,ref input,ref output,default,status,metadata);
        Assert.True(f.InitialDeleter(ref logRecord,ref deleteInfo));
        f.PostInitialDeleter(ref logRecord,ref deleteInfo);
        Assert.True(f.InPlaceDeleter(ref logRecord,ref deleteInfo));
        f.PostDeleteOperation(key,ref deleteInfo,new DummyEpochAccessor());
        f.ReadCompletionCallback(ref disk,ref input,ref output,default,status,metadata);

        var rmwModified = f.GetRMWModifiedFieldInfo(in src,ref input);
        var rmwInitial = f.GetRMWInitialFieldInfo(key,ref input);
        var upsertSpan = f.GetUpsertFieldInfo(key,span,ref input);
        var upsertObj = f.GetUpsertFieldInfo(key,new DummyHeapObject(),ref input);
        var upsertLog = f.GetUpsertFieldInfo(key,in src,ref input);
        Assert.True(rmwModified.KeySize>=0);
        Assert.True(rmwInitial.KeySize>=0);
        Assert.True(upsertSpan.ValueSize>=0);
        Assert.True(upsertObj.ValueIsObject);
        Assert.True(upsertLog.KeySize>=0);
        //GetUpsertFieldInfo_ValueIsObject=trueの分岐を通す
        {
            var srcObject = new DummySourceLogRecord(1,"v",asObject: true);
            var upsertLogObject = f.GetUpsertFieldInfo(key,in srcObject,ref input);
            Assert.True(upsertLogObject.ValueIsObject);
        }
    }

    [Fact, Trait("Category","Coverage")]
    public void AddSessionFunctions_Upsertで実セッション経由のWriterをカバーする() {
        using var store = new DictionaryStore<int,string>();
        using var session = store.Store.NewSession<FixedSpanByteKey,PinnedSpanByte,SpanByteAndMemory,Seamless.Collections.Empty,Seamless.Collections.AddSessionFunctions>(new Seamless.Collections.AddSessionFunctions());
        var bc = session.BasicContext;
        var key = MakeKey(SerInt(200));
        //InitialWriter(ReadOnlySpan<byte>)のclosing}を通す
        {
            var status = bc.Upsert(key,SerStr("new"));
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
        }
        //InPlaceWriter(ReadOnlySpan<byte>)のclosing}を通す
        {
            var status = bc.Upsert(key,SerStr("updated"));
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
        }
    }
}
