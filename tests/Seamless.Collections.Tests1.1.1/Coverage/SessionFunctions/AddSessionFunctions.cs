using Tsavorite.core;
using SSessionFunctions = Tsavorite.core.ISessionFunctions<Tsavorite.core.SpanByte,Tsavorite.core.SpanByte,Tsavorite.core.SpanByte,Tsavorite.core.SpanByteAndMemory,Seamless.Collections.Empty>;
namespace Seamless.Collections.Tests;

[Collection("Sequential")]
public class AddSessionFunctions {
    private readonly struct DummyEpochAccessor:IEpochAccessor {
        public void Resume() { }
        public bool TrySuspend() => true;
    }

    [Fact, Trait("Category","Coverage")]
    public void AddSessionFunctions_RMWで重複拒否できる() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        Assert.Equal(TryAddResult.Success,dict.TryAdd(1,"v1"));
        Assert.Equal(TryAddResult.KeyAlreadyExists,dict.TryAdd(1,"v2"));
    }

    //[Fact, Trait("Category","Coverage")]
    //public void AddSessionFunctions_24メソッドを直接実行する() {
    //    var f = new Seamless.Collections.AddSessionFunctions<int,string>();
    //    SSessionFunctions fi = f;
    //    var key = default(SpanByte);
    //    var input = default(SpanByte);
    //    var value = default(SpanByte);
    //    var src = default(SpanByte);
    //    var dst = default(SpanByte);
    //    SpanByteAndMemory output = default;
    //    var readInfo = default(ReadInfo);
    //    var recordInfo = default(RecordInfo);
    //    var upsertInfo = default(UpsertInfo);
    //    var rmwInfo = default(RMWInfo);
    //    var deleteInfo = default(DeleteInfo);
    //    var status = default(Status);
    //    var metadata = default(RecordMetadata);

    //    Assert.True(f.SingleReader(ref key,ref input,ref value,ref output,ref readInfo));
    //    Assert.True(f.ConcurrentReader(ref key,ref input,ref value,ref output,ref readInfo,ref recordInfo));
    //    f.ReadCompletionCallback(ref key,ref input,ref output,default,status,metadata);
    //    Assert.True(f.SingleWriter(ref key,ref input,ref src,ref dst,ref output,ref upsertInfo,WriteReason.Upsert,ref recordInfo));
    //    f.PostSingleWriter(ref key,ref input,ref src,ref dst,ref output,ref upsertInfo,WriteReason.Upsert);
    //    Assert.True(f.ConcurrentWriter(ref key,ref input,ref src,ref dst,ref output,ref upsertInfo,ref recordInfo));
    //    fi.PostUpsertOperation(ref key,ref input,ref src,ref upsertInfo,new DummyEpochAccessor());
    //    Assert.True(f.NeedInitialUpdate(ref key,ref input,ref output,ref rmwInfo));
    //    Assert.True(f.InitialUpdater(ref key,ref input,ref value,ref output,ref rmwInfo,ref recordInfo));
    //    f.PostInitialUpdater(ref key,ref input,ref value,ref output,ref rmwInfo);
    //    Assert.False(f.NeedCopyUpdate(ref key,ref input,ref value,ref output,ref rmwInfo));
    //    Assert.True(f.CopyUpdater(ref key,ref input,ref src,ref dst,ref output,ref rmwInfo,ref recordInfo));
    //    Assert.True(f.PostCopyUpdater(ref key,ref input,ref src,ref dst,ref output,ref rmwInfo));
    //    Assert.True(f.InPlaceUpdater(ref key,ref input,ref value,ref output,ref rmwInfo,ref recordInfo));
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

    [Fact, Trait("Category","Coverage")]
    public void AddSessionFunctions_Upsertで実セッション経由のWriterをカバーする() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();
        dict[200]="new";
        dict[200]="updated";
    }
}
