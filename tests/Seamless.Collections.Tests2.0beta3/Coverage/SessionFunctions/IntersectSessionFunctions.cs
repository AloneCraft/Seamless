using Common;
using Garnet.common;
using MemoryPack;
using Tsavorite.core;
using Xunit;
namespace Seamless.Collections.Tests;

[Collection("Sequential")]
public class IntersectSessionFunctions {
    private readonly struct DummyEpochAccessor:IEpochAccessor {
        public void Resume() { }
        public bool TrySuspend() => true;
    }
    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize = Const.MinObjectLogCapacity;
    private static FixedSpanByteKey MakeKey(int value) => (FixedSpanByteKey)(ReadOnlySpan<byte>)MemoryPackSerializer.Serialize(value);

    [Fact, Trait("Category","Coverage")]
    public void IntersectSessionFunctions_IntersectWithで共通要素だけ残る() {
        using var storeA = new HashSetStore<int>();
        using var storeB = new HashSetStore<int>();
        using var setA = storeA.Create();
        using var setB = storeB.Create();
        setA.Add(1);
        setA.Add(2);
        setA.Add(3);
        setB.Add(2);
        setB.Add(3);
        setB.Add(4);

        setA.IntersectWith(setB);

        Assert.DoesNotContain(1,setA);
        Assert.Contains(2,setA);
        Assert.Contains(3,setA);
        Assert.DoesNotContain(4,setA);
    }

    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void IntersectSessionFunctions_IsPending経路でも動作する() {
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
            if(a%2==0) {
                setB.Add(a);
            }
        }

        setA.IntersectWith(setB);

        Assert.Contains(100,setA);
        Assert.DoesNotContain(101,setA);
    }

    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void ConvertOutputToHeap() {
        var folderA = CreateFolder();
        using var logDeviceA = new ManagedLocalStorageDevice(Path.Combine(folderA,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDeviceA = new ManagedLocalStorageDevice(Path.Combine(folderA+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var storeA = new HashSetStore<int>(logDeviceA,objDeviceA,MemorySize: TestMemorySize,MutableFraction: 0);
        using var storeB = new HashSetStore<int>();
        using var setA = storeA.Create();
        using var setB = storeB.Create();

        for(var a = 0; a<300; a++) {
            setA.Add(a);
            if(a%2==0) {
                setB.Add(a);
            }
        }

        setA.IntersectWith(setB);

        Assert.Contains(100,setA);
        Assert.DoesNotContain(101,setA);
    }

    //[Fact, Trait("Category","Coverage")]
    //public void IntersectSessionFunctions_35メソッドを直接実行する() {
    //    var f = new Seamless.Collections.IntersectSessionFunctions();
    //    var disk = default(DiskLogRecord);
    //    var src = new DummySourceLogRecord(1,"v");
    //    var key = MakeKey(13);
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
    //    Assert.False(f.NeedInitialUpdate(key,ref input,ref output,ref rmwInfo));
    //    Assert.False(f.InitialUpdater(ref logRecord,in sizeInfo,ref input,ref output,ref rmwInfo));
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
