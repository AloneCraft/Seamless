using Common;
namespace Seamless.Collections.Tests;

[Collection("Sequential")]
public class DeleteSessionFunctions{
    private static string CreateFolder()=>Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize=Const.MinObjectLogCapacity;

    //[Fact, Trait("Category","Coverage")]
    //public void DeleteSessionFunctions_Removeで既存と未存在を判定できる()
    //    using var store = new DictionaryStore<int,string>();
    //    using var dict = store.Create();

    //    dict[1]="one";
    //    Assert.True(dict.Remove(1));
    //    Assert.False(dict.Remove(1));
    //    Assert.False(dict.Remove(9999));
    //}

    //[Fact, Trait("Category","Coverage")]
    //[System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    //public void DeleteSessionFunctions_IsPendingCopyUpdaterパス() {
    //    var folder = CreateFolder();
    //    using var logDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
    //    using var objDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
    //    using var store = new DictionaryStore<int,string>(logDevice,objDevice,MemorySize: TestMemorySize,MutableFraction: 0);
    //    using var dict = store.Create();

    //    for(var a = 0; a<500; a++) {
    //        dict[a]=$"v{a}";
    //    }
    //    for(var a = 0; a<100; a++) {
    //        dict.Remove(a);
    //    }

    //    Assert.False(dict.Remove(99999));
    //}

    //[Fact, Trait("Category","Coverage")]
    //public void DeleteSessionFunctions_ConvertOutputToHeapはnoopである() {
    //    var f = new Seamless.Collections.DeleteSessionFunctions<int>();
    //    var input = default(SpanByte);
    //    SpanByteAndMemory output = default;
    //    f.ConvertOutputToHeap(ref input,ref output);
    //}

    //[Fact,Trait("Category","Coverage")]
    //public void DeleteSessionFunctions_24メソッドを直接実行する(){
    //    var f=new Seamless.Collections.DeleteSessionFunctions<int>();
    //    SSessionFunctions fi=f;
    //    var key=default(SpanByte);
    //    var input=default(SpanByte);
    //    var value=default(SpanByte);
    //    var src=default(SpanByte);
    //    var dst=default(SpanByte);
    //    SpanByteAndMemory output=default;
    //    var readInfo=default(ReadInfo);
    //    var recordInfo=default(RecordInfo);
    //    var upsertInfo=default(UpsertInfo);
    //    var rmwInfo=default(RMWInfo);
    //    var deleteInfo=default(DeleteInfo);
    //    var status=default(Status);
    //    var metadata=default(RecordMetadata);

    //    Assert.True(f.SingleReader(ref key,ref input,ref value,ref output,ref readInfo));
    //    Assert.True(f.ConcurrentReader(ref key,ref input,ref value,ref output,ref readInfo,ref recordInfo));
    //    f.ReadCompletionCallback(ref key,ref input,ref output,default,status,metadata);
    //    Assert.True(f.SingleWriter(ref key,ref input,ref src,ref dst,ref output,ref upsertInfo,WriteReason.Upsert,ref recordInfo));
    //    f.PostSingleWriter(ref key,ref input,ref src,ref dst,ref output,ref upsertInfo,WriteReason.Upsert);
    //    Assert.True(f.ConcurrentWriter(ref key,ref input,ref src,ref dst,ref output,ref upsertInfo,ref recordInfo));
    //    fi.PostUpsertOperation(ref key,ref input,ref src,ref upsertInfo,new DummyEpochAccessor());
    //    Assert.True(f.NeedInitialUpdate(ref key,ref input,ref output,ref rmwInfo));
    //    Assert.False(f.InitialUpdater(ref key,ref input,ref value,ref output,ref rmwInfo,ref recordInfo));
    //    f.PostInitialUpdater(ref key,ref input,ref value,ref output,ref rmwInfo);
    //    Assert.True(f.NeedCopyUpdate(ref key,ref input,ref value,ref output,ref rmwInfo));
    //    rmwInfo=default;
    //    Assert.False(f.CopyUpdater(ref key,ref input,ref src,ref dst,ref output,ref rmwInfo,ref recordInfo));
    //    Assert.Equal(RMWAction.ExpireAndStop,rmwInfo.Action);
    //    Assert.True(f.PostCopyUpdater(ref key,ref input,ref src,ref dst,ref output,ref rmwInfo));
    //    rmwInfo=default;
    //    Assert.False(f.InPlaceUpdater(ref key,ref input,ref value,ref output,ref rmwInfo,ref recordInfo));
    //    Assert.Equal(RMWAction.ExpireAndStop,rmwInfo.Action);
    //    Assert.True(f.GetRMWModifiedValueLength(ref value,ref input)>=0);
    //    Assert.True(f.GetRMWInitialValueLength(ref input)>=0);
    //    Assert.True(f.GetUpsertValueLength(ref value,ref input)>=0);
    //    fi.PostRMWOperation(ref key,ref input,ref rmwInfo,new DummyEpochAccessor());
    //    f.RMWCompletionCallback(ref key,ref input,ref output,default,status,metadata);
    //    Assert.True(f.SingleDeleter(ref key,ref value,ref deleteInfo,ref recordInfo));
    //    f.PostSingleDeleter(ref key,ref deleteInfo);
    //    Assert.True(f.ConcurrentDeleter(ref key,ref value,ref deleteInfo,ref recordInfo));
    //    fi.PostDeleteOperation(ref key,ref deleteInfo,new DummyEpochAccessor());
    //    f.ConvertOutputToHeap(ref input,ref output);
    //}
}
