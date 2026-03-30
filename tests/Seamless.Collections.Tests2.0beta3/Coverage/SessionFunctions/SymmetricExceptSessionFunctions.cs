using Common;
using Garnet.common;
using MemoryPack;
using Tsavorite.core;
using Xunit;
namespace Seamless.Collections.Tests;

[Collection("Sequential")]
public class SymmetricExceptSessionFunctions {
    private readonly struct DummyEpochAccessor:IEpochAccessor {
        public void Resume() { }
        public bool TrySuspend() => true;
    }
    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize = Const.MinObjectLogCapacity;
    private static FixedSpanByteKey MakeKey(int value) => (FixedSpanByteKey)(ReadOnlySpan<byte>)MemoryPackSerializer.Serialize(value);

    [Fact, Trait("Category","Coverage")]
    public void SymmetricExceptSessionFunctions_SymmetricExceptWithで排他的論理和になる() {
        using var storeA = new HashSetStore<int>();
        using var storeB = new HashSetStore<int>();
        using var setA = storeA.Create();
        using var setB = storeB.Create();
        setA.Add(1);
        setA.Add(2);
        setA.Add(3);
        setB.Add(3);
        setB.Add(4);

        setA.SymmetricExceptWith(setB);

        Assert.Contains(1,setA);
        Assert.Contains(2,setA);
        Assert.DoesNotContain(3,setA);
        Assert.Contains(4,setA);
    }

    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void SymmetricExceptSessionFunctions_IsPending経路でも動作する() {
        var folderA = CreateFolder();
        var folderB = CreateFolder();
        using var logDeviceA = new ManagedLocalStorageDevice(Path.Combine(folderA,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDeviceA = new ManagedLocalStorageDevice(Path.Combine(folderA+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var logDeviceB = new ManagedLocalStorageDevice(Path.Combine(folderB,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDeviceB = new ManagedLocalStorageDevice(Path.Combine(folderB+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var storeA = new HashSetStore<int>(logDeviceA,objDeviceA,MemorySize: TestMemorySize,MutableFraction: 0);
        using var storeB = new HashSetStore<int>(logDeviceB,objDeviceB,MemorySize: TestMemorySize,MutableFraction: 0);
        using var setA = storeA.Create();
        using var setB = storeB.Create();

        for(var a = 0; a<300; a++) {
            setA.Add(a);
            if(a%3==0) {
                setB.Add(a);
            }
        }

        setA.SymmetricExceptWith(setB);

        Assert.DoesNotContain(0,setA);
        Assert.Contains(1,setA);
    }

    [Fact, Trait("Category","Coverage")]
    public void SymmetricExceptSessionFunctions_ConvertOutputToHeapは例外を投げる() {
        var sessionFunctions = new Seamless.Collections.SymmetricExcept_SessionFunctions();
        var input = default(PinnedSpanByte);
        SpanByteAndMemory output = default;
        Assert.Throws<NotSupportedException>(() => sessionFunctions.ConvertOutputToHeap(ref input,ref output));
    }

    //[Fact, Trait("Category","Coverage")]
    //public void SymmetricExceptSessionFunctions_35メソッドを直接実行する() {
    //    var f = new Seamless.Collections.SymmetricExceptSessionFunctions();
    //    var disk = default(DiskLogRecord);
    //    var src = new DummySourceLogRecord(1,"v");
    //    var key = MakeKey(14);
    //    var span = MemoryPackSerializer.Serialize("x");
    //    var logRecord = default(LogRecord);
    //    var sizeInfo = default(RecordSizeInfo);
    //    var input = default(PinnedSpanByte);
    //    SpanByteAndMemory output = default;
    //    var readInfo = default(ReadInfo);
    //    var upsertInfo = default(UpsertInfo);
    //    var rmwInfo = default(RMWInfo);
    //    var deleteInfo = default(DeleteInfo);
    //    var status = default(Status);
    //    var metadata = default(RecordMetadata);

    //    Assert.Throws<NotSupportedException>(() => f.Reader(in disk,ref input,ref output,ref readInfo));
    //    Assert.Throws<NotSupportedException>(() => f.InPlaceWriter(ref logRecord,in sizeInfo,ref input,span,ref output,ref upsertInfo));
    //    Assert.Throws<NotSupportedException>(() => { _=f.InPlaceWriter(ref logRecord,in sizeInfo,ref input,(IHeapObject)null!,ref output,ref upsertInfo); });
    //    Assert.Throws<NotSupportedException>(() => { _=f.InPlaceWriter(ref logRecord,in sizeInfo,ref input,in disk,ref output,ref upsertInfo); });
    //    Assert.Throws<NotSupportedException>(() => f.InitialWriter(ref logRecord,in sizeInfo,ref input,span,ref output,ref upsertInfo));
    //    Assert.Throws<NotSupportedException>(() => { _=f.InitialWriter(ref logRecord,in sizeInfo,ref input,(IHeapObject)null!,ref output,ref upsertInfo); });
    //    Assert.Throws<NotSupportedException>(() => { _=f.InitialWriter(ref logRecord,in sizeInfo,ref input,in disk,ref output,ref upsertInfo); });
    //    f.PostUpsertOperation(key,ref input,span,ref upsertInfo,new DummyEpochAccessor());
    //    f.PostUpsertOperation(key,ref input,(IHeapObject)null!,ref upsertInfo,new DummyEpochAccessor());
    //    f.PostInitialWriter(ref logRecord,in sizeInfo,ref input,span,ref output,ref upsertInfo);
    //    f.PostInitialWriter(ref logRecord,in sizeInfo,ref input,(IHeapObject)null!,ref output,ref upsertInfo);
    //    f.PostInitialWriter(ref logRecord,in sizeInfo,ref input,in disk,ref output,ref upsertInfo);
    //    Assert.True(f.NeedInitialUpdate(key,ref input,ref output,ref rmwInfo));
    //    _=Record.Exception(() => f.InitialUpdater(ref logRecord,in sizeInfo,ref input,ref output,ref rmwInfo));
    //    Assert.False(f.InPlaceUpdater(ref logRecord,in sizeInfo,ref input,ref output,ref rmwInfo));
    //    Assert.Equal(RMWAction.ExpireAndStop,rmwInfo.Action);
    //    f.PostInitialUpdater(ref logRecord,in sizeInfo,ref input,ref output,ref rmwInfo);
    //    Assert.True(f.NeedCopyUpdate(in src,ref input,ref output,ref rmwInfo));
    //    rmwInfo=default;
    //    Assert.False(f.CopyUpdater(in src,ref logRecord,in sizeInfo,ref input,ref output,ref rmwInfo));
    //    Assert.Equal(RMWAction.ExpireAndStop,rmwInfo.Action);
    //    Assert.Throws<NotSupportedException>(() => f.PostCopyUpdater(in src,ref logRecord,in sizeInfo,ref input,ref output,ref rmwInfo));
    //    f.PostRMWOperation(key,ref input,ref rmwInfo,new DummyEpochAccessor());
    //    Assert.Throws<NotSupportedException>(() => f.RMWCompletionCallback(ref disk,ref input,ref output,default,status,metadata));
    //    Assert.Throws<NotSupportedException>(() => f.InitialDeleter(ref logRecord,ref deleteInfo));
    //    Assert.Throws<NotSupportedException>(() => f.PostInitialDeleter(ref logRecord,ref deleteInfo));
    //    Assert.True(f.InPlaceDeleter(ref logRecord,ref deleteInfo));
    //    {
    //        var thrown = false;
    //        try { f.PostDeleteOperation(key,ref deleteInfo,new DummyEpochAccessor()); } catch(NotSupportedException) { thrown=true; }
    //        Assert.True(thrown);
    //    }
    //    Assert.Throws<NotSupportedException>(() => f.ReadCompletionCallback(ref disk,ref input,ref output,default,status,metadata));

    //    var rmwModified = f.GetRMWModifiedFieldInfo(in src,ref input);
    //    var rmwInitial = f.GetRMWInitialFieldInfo(key,ref input);
    //    Assert.True(rmwModified.KeySize>=0);
    //    Assert.True(rmwInitial.KeySize>=0);
    //    {
    //        var thrown = false;
    //        try { f.GetUpsertFieldInfo(key,span,ref input); } catch(NotSupportedException) { thrown=true; }
    //        Assert.True(thrown);
    //    }
    //    {
    //        var thrown = false;
    //        try { f.GetUpsertFieldInfo(key,(IHeapObject)null!,ref input); } catch(NotSupportedException) { thrown=true; }
    //        Assert.True(thrown);
    //    }
    //    {
    //        var thrown = false;
    //        try { f.GetUpsertFieldInfo(key,in src,ref input); } catch(NotSupportedException) { thrown=true; }
    //        Assert.True(thrown);
    //    }
    //    Assert.Throws<NotSupportedException>(() => f.ConvertOutputToHeap(ref input,ref output));
    //}
}
