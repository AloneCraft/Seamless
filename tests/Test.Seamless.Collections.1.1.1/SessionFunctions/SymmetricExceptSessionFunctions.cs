using Common;
using Seamless.Collections;
using Tsavorite.core;
using SSessionFunctions = Tsavorite.core.ISessionFunctions<Tsavorite.core.SpanByte,Tsavorite.core.SpanByte,Tsavorite.core.SpanByte,Tsavorite.core.SpanByteAndMemory,Seamless.Collections.Empty>;
namespace Test.Seamless.SessionFunctions;

[Collection("Sequential")]
public class SymmetricExceptSessionFunctions {
    private readonly struct DummyEpochAccessor:IEpochAccessor {
        public void Resume() { }
        public bool TrySuspend() => true;
    }
    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize = Const.MinObjectLogCapacity;

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
    public void SymmetricExceptSessionFunctions_ConvertOutputToHeapはnoopである() {
        var f = new global::Seamless.SessionFunctions.SymmetricExceptWith();
        var input = default(SpanByte);
        SpanByteAndMemory output = default;
        f.ConvertOutputToHeap(ref input,ref output);
    }

    [Fact, Trait("Category","Coverage")]
    public void SymmetricExceptSessionFunctions_24メソッドを直接実行する() {
        var f = new global::Seamless.SessionFunctions.SymmetricExceptWith();
        SSessionFunctions fi = f;
        var key = default(SpanByte);
        var input = default(SpanByte);
        var value = default(SpanByte);
        var src = default(SpanByte);
        var dst = default(SpanByte);
        SpanByteAndMemory output = default;
        var readInfo = default(ReadInfo);
        var recordInfo = default(RecordInfo);
        var upsertInfo = default(UpsertInfo);
        var rmwInfo = default(RMWInfo);
        var deleteInfo = default(DeleteInfo);
        var status = default(Status);
        var metadata = default(RecordMetadata);

        Assert.True(f.SingleReader(ref key,ref input,ref value,ref output,ref readInfo));
        Assert.True(f.ConcurrentReader(ref key,ref input,ref value,ref output,ref readInfo,ref recordInfo));
        f.ReadCompletionCallback(ref key,ref input,ref output,default,status,metadata);
        Assert.True(f.SingleWriter(ref key,ref input,ref src,ref dst,ref output,ref upsertInfo,WriteReason.Upsert,ref recordInfo));
        f.PostSingleWriter(ref key,ref input,ref src,ref dst,ref output,ref upsertInfo,WriteReason.Upsert);
        Assert.True(f.ConcurrentWriter(ref key,ref input,ref src,ref dst,ref output,ref upsertInfo,ref recordInfo));
        fi.PostUpsertOperation(ref key,ref input,ref src,ref upsertInfo,new DummyEpochAccessor());
        Assert.True(f.NeedInitialUpdate(ref key,ref input,ref output,ref rmwInfo));
        Assert.True(f.InitialUpdater(ref key,ref input,ref value,ref output,ref rmwInfo,ref recordInfo));
        f.PostInitialUpdater(ref key,ref input,ref value,ref output,ref rmwInfo);
        Assert.True(f.NeedCopyUpdate(ref key,ref input,ref value,ref output,ref rmwInfo));
        rmwInfo=default;
        Assert.False(f.CopyUpdater(ref key,ref input,ref src,ref dst,ref output,ref rmwInfo,ref recordInfo));
        Assert.Equal(RMWAction.ExpireAndStop,rmwInfo.Action);
        Assert.True(f.PostCopyUpdater(ref key,ref input,ref src,ref dst,ref output,ref rmwInfo));
        rmwInfo=default;
        Assert.False(f.InPlaceUpdater(ref key,ref input,ref value,ref output,ref rmwInfo,ref recordInfo));
        Assert.Equal(RMWAction.ExpireAndStop,rmwInfo.Action);
        Assert.True(f.GetRMWModifiedValueLength(ref value,ref input)>=0);
        Assert.True(f.GetRMWInitialValueLength(ref input)>=0);
        Assert.True(f.GetUpsertValueLength(ref value,ref input)>=0);
        fi.PostRMWOperation(ref key,ref input,ref rmwInfo,new DummyEpochAccessor());
        f.RMWCompletionCallback(ref key,ref input,ref output,default,status,metadata);
        Assert.True(f.SingleDeleter(ref key,ref value,ref deleteInfo,ref recordInfo));
        f.PostSingleDeleter(ref key,ref deleteInfo);
        Assert.True(f.ConcurrentDeleter(ref key,ref value,ref deleteInfo,ref recordInfo));
        fi.PostDeleteOperation(ref key,ref deleteInfo,new DummyEpochAccessor());
        f.ConvertOutputToHeap(ref input,ref output);
    }
}
