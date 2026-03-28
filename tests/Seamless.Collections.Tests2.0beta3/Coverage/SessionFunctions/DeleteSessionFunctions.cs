using Common;
using Garnet.common;
using MemoryPack;
using Tsavorite.core;
using Xunit;
namespace Seamless.Collections.Tests;

[Collection("Sequential")]
public class DeleteSessionFunctions {
    private readonly struct DummyEpochAccessor:IEpochAccessor {
        public void Resume() { }
        public bool TrySuspend() => true;
    }
    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize = Const.MinObjectLogCapacity;
    private static FixedSpanByteKey MakeKey(int value) => (FixedSpanByteKey)(ReadOnlySpan<byte>)MemoryPackSerializer.Serialize(value);

    [Fact, Trait("Category","Coverage")]
    public void DeleteSessionFunctions_Removeで既存と未存在を判定できる() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();

        dict[1]="one";
        Assert.True(dict.Remove(1));
        Assert.False(dict.Remove(1));
        Assert.False(dict.Remove(9999));
    }

    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void DeleteSessionFunctions_IsPendingCopyUpdaterパス() {
        var folder = CreateFolder();
        using var logDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var store = new DictionaryStore<int,string>(logDevice,objDevice,MemorySize: TestMemorySize,MutableFraction: 0);
        using var dict = store.Create();

        for(var a = 0; a<500; a++) {
            dict[a]=$"v{a}";
        }
        for(var a = 0; a<100; a++) {
            dict.Remove(a);
        }

        Assert.False(dict.Remove(99999));
    }

    [Fact, Trait("Category","Coverage")]
    public void DeleteSessionFunctions_ConvertOutputToHeapは例外を投げる() {
        var sessionFunctions = new Seamless.Collections.DeleteSessionFunctions();
        var input = default(PinnedSpanByte);
        SpanByteAndMemory output = default;
        Assert.Throws<NotSupportedException>(() => sessionFunctions.ConvertOutputToHeap(ref input,ref output));
    }

    [Fact, Trait("Category","Coverage")]
    public void DeleteSessionFunctions_35メソッドを直接実行する() {
        var f = new Seamless.Collections.DeleteSessionFunctions();
        var disk = default(DiskLogRecord);
        var src = new DummySourceLogRecord(1,"v");
        var key = MakeKey(12);
        var span = MemoryPackSerializer.Serialize("x");
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

        Assert.Throws<NotSupportedException>(() => f.Reader(in disk,ref input,ref output,ref readInfo));
        Assert.Throws<NotSupportedException>(() => f.InPlaceWriter(ref logRecord,in sizeInfo,ref input,span,ref output,ref upsertInfo));
        Assert.Throws<NotSupportedException>(() => { _=f.InPlaceWriter(ref logRecord,in sizeInfo,ref input,(IHeapObject)null!,ref output,ref upsertInfo); });
        Assert.Throws<NotSupportedException>(() => { _=f.InPlaceWriter(ref logRecord,in sizeInfo,ref input,in src,ref output,ref upsertInfo); });
        Assert.Throws<NotSupportedException>(() => f.InitialWriter(ref logRecord,in sizeInfo,ref input,span,ref output,ref upsertInfo));
        Assert.Throws<NotSupportedException>(() => { _=f.InitialWriter(ref logRecord,in sizeInfo,ref input,(IHeapObject)null!,ref output,ref upsertInfo); });
        Assert.Throws<NotSupportedException>(() => { _=f.InitialWriter(ref logRecord,in sizeInfo,ref input,in src,ref output,ref upsertInfo); });
        f.PostUpsertOperation(key,ref input,span,ref upsertInfo,new DummyEpochAccessor());
        f.PostUpsertOperation(key,ref input,(IHeapObject)null!,ref upsertInfo,new DummyEpochAccessor());
        f.PostInitialWriter(ref logRecord,in sizeInfo,ref input,span,ref output,ref upsertInfo);
        f.PostInitialWriter(ref logRecord,in sizeInfo,ref input,(IHeapObject)null!,ref output,ref upsertInfo);
        f.PostInitialWriter(ref logRecord,in sizeInfo,ref input,in src,ref output,ref upsertInfo);
        Assert.False(f.NeedInitialUpdate(key,ref input,ref output,ref rmwInfo));
        Assert.False(f.InitialUpdater(ref logRecord,in sizeInfo,ref input,ref output,ref rmwInfo));
        Assert.False(f.InPlaceUpdater(ref logRecord,in sizeInfo,ref input,ref output,ref rmwInfo));
        Assert.Equal(RMWAction.ExpireAndStop,rmwInfo.Action);
        f.PostInitialUpdater(ref logRecord,in sizeInfo,ref input,ref output,ref rmwInfo);
        Assert.True(f.NeedCopyUpdate(in src,ref input,ref output,ref rmwInfo));
        rmwInfo=default;
        Assert.False(f.CopyUpdater(in src,ref logRecord,in sizeInfo,ref input,ref output,ref rmwInfo));
        Assert.Equal(RMWAction.ExpireAndStop,rmwInfo.Action);
        Assert.Throws<NotSupportedException>(() => f.PostCopyUpdater(in src,ref logRecord,in sizeInfo,ref input,ref output,ref rmwInfo));
        f.PostRMWOperation(key,ref input,ref rmwInfo,new DummyEpochAccessor());
        Assert.Throws<NotSupportedException>(() => f.RMWCompletionCallback(ref disk,ref input,ref output,default,status,metadata));
        Assert.Throws<NotSupportedException>(() => f.InitialDeleter(ref logRecord,ref deleteInfo));
        Assert.Throws<NotSupportedException>(() => f.PostInitialDeleter(ref logRecord,ref deleteInfo));
        Assert.True(f.InPlaceDeleter(ref logRecord,ref deleteInfo));
        {
            var thrown = false;
            try { f.PostDeleteOperation(key,ref deleteInfo,new DummyEpochAccessor()); } catch(NotSupportedException) { thrown=true; }
            Assert.True(thrown);
        }
        Assert.Throws<NotSupportedException>(() => f.ReadCompletionCallback(ref disk,ref input,ref output,default,status,metadata));

        var rmwModified = f.GetRMWModifiedFieldInfo(in src,ref input);
        var rmwInitial = f.GetRMWInitialFieldInfo(key,ref input);
        Assert.True(rmwModified.KeySize>=0);
        Assert.True(rmwInitial.KeySize>=0);
        {
            var thrown = false;
            try { f.GetUpsertFieldInfo(key,span,ref input); } catch(NotSupportedException) { thrown=true; }
            Assert.True(thrown);
        }
        {
            var thrown = false;
            try { f.GetUpsertFieldInfo(key,(IHeapObject)null!,ref input); } catch(NotSupportedException) { thrown=true; }
            Assert.True(thrown);
        }
        {
            var thrown = false;
            try { f.GetUpsertFieldInfo(key,in src,ref input); } catch(NotSupportedException) { thrown=true; }
            Assert.True(thrown);
        }
        Assert.Throws<NotSupportedException>(() => f.ConvertOutputToHeap(ref input,ref output));
    }
}
