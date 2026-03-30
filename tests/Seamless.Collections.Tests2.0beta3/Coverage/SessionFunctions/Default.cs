using Tsavorite.core;
using Common;
using Garnet.common;
using MemoryPack;
using Seamless.Collections;
namespace Seamless.SessionFunctions.Tests;


public class Default {
    private static string CreateFolder()=>Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize=Const.MinObjectLogCapacity;
    private static byte[] SerInt(int v)=>MemoryPackSerializer.Serialize(v);
    private static byte[] SerStr(string v)=>MemoryPackSerializer.Serialize(v);
    private static FixedSpanByteKey MakeKey(byte[] b)=>(FixedSpanByteKey)(ReadOnlySpan<byte>)b;
    [Fact,Trait("Category","Coverage")]
    public void DefaultSessionFunctions_通常パス() {
        using var store=new DictionaryStore<int,string>();
        using var session= store.Store.NewSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, Seamless.Collections.Empty, SessionFunctions.Default>(new SessionFunctions.Default());
        var bc=session.BasicContext;

        {
            var status=bc.Upsert(MakeKey(SerInt(1)),SerStr("hello"));
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
        }
        {
            SpanByteAndMemory output=default;
            var status=bc.Read(MakeKey(SerInt(1)),ref output);
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Next();
                status=completed.Current.Status;
                completed.Dispose();
            }
            Assert.False(status.NotFound);
        }
        {
            var status=bc.Upsert(MakeKey(SerInt(1)),SerStr("world"));
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
        }
        {
            var key=MakeKey(SerInt(2));
            var input=PinnedSpanByte.FromPinnedSpan(SerStr("rmw_new"));
            SpanByteAndMemory output=default;
            var status=bc.RMW(key,ref input,ref output);
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
        }
        {
            var key=MakeKey(SerInt(2));
            var input=PinnedSpanByte.FromPinnedSpan(SerStr("rmw_update"));
            SpanByteAndMemory output=default;
            var status=bc.RMW(key,ref input,ref output);
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
        }
        {
            var status=bc.Delete(MakeKey(SerInt(999)));
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
        }
        {
            var status=bc.Delete(MakeKey(SerInt(1)));
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
        }
    }

    [Fact,Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void DefaultSessionFunctions_IsPendingパス() {
        var folder=CreateFolder();
        using var logDevice=new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice=new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var store=new DictionaryStore<int,string>(logDevice,objDevice,MemorySize: TestMemorySize,MutableFraction: 0);
        using var session= store.Store.NewSession<FixedSpanByteKey, PinnedSpanByte, SpanByteAndMemory, Seamless.Collections.Empty, SessionFunctions.Default>(new SessionFunctions.Default());
        var bc=session.BasicContext;

        for(var a=0; a<300; a++) {
            var status=bc.Upsert(MakeKey(SerInt(a)),SerStr($"v{a}"));
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Dispose();
            }
        }
        for(var a=0; a<100; a++) {
            var key=MakeKey(SerInt(a));
            var input=PinnedSpanByte.FromPinnedSpan(SerStr($"upd{a}"));
            SpanByteAndMemory output=default;
            var status=bc.RMW(key,ref input,ref output);
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Next();
                completed.Dispose();
            }
        }
        for(var a=100; a<200; a++) {
            var key=MakeKey(SerInt(a));
            SpanByteAndMemory output=default;
            var status=bc.Read(key,ref output);
            if(status.IsPending) {
                bc.CompletePendingWithOutputs(out var completed,wait: true);
                completed.Next();
                completed.Dispose();
            }
        }
    }

    //[Fact,Trait("Category","Coverage")]
    //public void DefaultSessionFunctions_35メソッドを直接実行する() {
    //    var f=new Seamless.Collections.DefaultSessionFunctions();
    //    var disk=default(DiskLogRecord);
    //    var src=new DummySourceLogRecord(1,"v");
    //    var key=MakeKey(SerInt(11));
    //    var span=SerStr("x");
    //    var logRecord=default(LogRecord);
    //    var sizeInfo=default(RecordSizeInfo);
    //    var input=default(PinnedSpanByte);
    //    SpanByteAndMemory output=default;
    //    var readInfo=default(ReadInfo);
    //    var upsertInfo=default(UpsertInfo);
    //    var rmwInfo=default(RMWInfo);
    //    var deleteInfo=default(DeleteInfo);
    //    var status=default(Status);
    //    var metadata=default(RecordMetadata);

    //    Assert.True(f.Reader(in src,ref input,ref output,ref readInfo));
    //    _=Record.Exception(()=>f.InPlaceWriter(ref logRecord,in sizeInfo,ref input,span,ref output,ref upsertInfo));
    //    _=Record.Exception(()=>f.InPlaceWriter(ref logRecord,in sizeInfo,ref input,new DummyHeapObject(),ref output,ref upsertInfo));
    //    _=Record.Exception(()=>f.InPlaceWriter(ref logRecord,in sizeInfo,ref input,in src,ref output,ref upsertInfo));
    //    _=Record.Exception(()=>f.InitialWriter(ref logRecord,in sizeInfo,ref input,span,ref output,ref upsertInfo));
    //    _=Record.Exception(()=>f.InitialWriter(ref logRecord,in sizeInfo,ref input,(IHeapObject)null!,ref output,ref upsertInfo));
    //    _=Record.Exception(()=>f.InitialWriter(ref logRecord,in sizeInfo,ref input,in src,ref output,ref upsertInfo));
    //    f.PostUpsertOperation(key,ref input,span,ref upsertInfo,new DummyEpochAccessor());
    //    f.PostUpsertOperation(key,ref input,new DummyHeapObject(),ref upsertInfo,new DummyEpochAccessor());
    //    f.PostInitialWriter(ref logRecord,in sizeInfo,ref input,span,ref output,ref upsertInfo);
    //    f.PostInitialWriter(ref logRecord,in sizeInfo,ref input,new DummyHeapObject(),ref output,ref upsertInfo);
    //    f.PostInitialWriter(ref logRecord,in sizeInfo,ref input,in src,ref output,ref upsertInfo);
    //    Assert.True(f.NeedInitialUpdate(key,ref input,ref output,ref rmwInfo));
    //    _=Record.Exception(()=>f.InitialUpdater(ref logRecord,in sizeInfo,ref input,ref output,ref rmwInfo));
    //    _=Record.Exception(()=>f.InPlaceUpdater(ref logRecord,in sizeInfo,ref input,ref output,ref rmwInfo));
    //    f.PostInitialUpdater(ref logRecord,in sizeInfo,ref input,ref output,ref rmwInfo);
    //    Assert.True(f.NeedCopyUpdate(in src,ref input,ref output,ref rmwInfo));
    //    _=Record.Exception(()=>f.CopyUpdater(in src,ref logRecord,in sizeInfo,ref input,ref output,ref rmwInfo));
    //    Assert.True(f.PostCopyUpdater(in src,ref logRecord,in sizeInfo,ref input,ref output,ref rmwInfo));
    //    f.PostRMWOperation(key,ref input,ref rmwInfo,new DummyEpochAccessor());
    //    f.RMWCompletionCallback(ref disk,ref input,ref output,default,status,metadata);
    //    Assert.True(f.InitialDeleter(ref logRecord,ref deleteInfo));
    //    f.PostInitialDeleter(ref logRecord,ref deleteInfo);
    //    Assert.True(f.InPlaceDeleter(ref logRecord,ref deleteInfo));
    //    f.PostDeleteOperation(key,ref deleteInfo,new DummyEpochAccessor());
    //    f.ReadCompletionCallback(ref disk,ref input,ref output,default,status,metadata);

    //    var rmwModified=f.GetRMWModifiedFieldInfo(in src,ref input);
    //    var rmwInitial=f.GetRMWInitialFieldInfo(key,ref input);
    //    var upsertSpan=f.GetUpsertFieldInfo(key,span,ref input);
    //    var upsertObj=f.GetUpsertFieldInfo(key,new DummyHeapObject(),ref input);
    //    var upsertLog=f.GetUpsertFieldInfo(key,in src,ref input);
    //    f.ConvertOutputToHeap(ref input,ref output);
    //    Assert.True(rmwModified.KeySize>=0);
    //    Assert.True(rmwInitial.KeySize>=0);
    //    Assert.True(upsertSpan.ValueSize>=0);
    //    Assert.True(upsertObj.ValueIsObject);
    //    Assert.True(upsertLog.KeySize>=0);
    //    //GetUpsertFieldInfo_ValueIsObject=true の分岐を通す
    //    {
    //        var srcObject=new DummySourceLogRecord(1,"v",asObject: true);
    //        var upsertLogObject=f.GetUpsertFieldInfo(key,in srcObject,ref input);
    //        Assert.True(upsertLogObject.ValueIsObject);
    //    }
    //}
}
