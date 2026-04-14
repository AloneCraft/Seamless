using Seamless;
using Seamless.Stores;
using Tsavorite.core;
using EqualityEmpty=Seamless.Collections.EqualityEmpty;
using ManagedOutput=Tsavorite.core.SpanByteAndMemory;
using ManagedSessionValue=Tsavorite.core.SpanByte;

namespace Test.Seamless.SessionFunctions;

public sealed class ManagedRead {
    private readonly struct DummyEpochAccessor:IEpochAccessor {
        public void Resume() { }
        public bool TrySuspend() => true;
    }

    private static (IDictionary<int,string> Dict,ManagedCollectionStore<int,string> Store) CreateMemory() {
        var store=Factory.Create().Create<int,string>();
        return (store.Create(),store);
    }

    private static (IDictionary<int,string> Dict,ManagedCollectionStore<int,string> Store) CreateDisk(IDevice logDevice,IDevice objDevice) {
        var store=Factory.Create(MemorySize: Const.MinObjectLogCapacity).Create<int,string>(logDevice,objDevice);
        return (store.Create(),store);
    }

    private static string CreateDiskFolder() =>
        Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));

    [Fact, Trait("Category","Unit")]
    public void TryGetValue_可変領域で読める() {
        var (dict,store)=CreateMemory();
        using var _store=store;
        using var _dict=(IDisposable)dict;

        dict[1]="value";

        Assert.True(dict.TryGetValue(1,out var actual));
        Assert.Equal("value",actual);
        Assert.False(dict.TryGetValue(999,out _));
    }

    [Fact, Trait("Category","Unit")]
    public void TryGetValue_ReadOnly領域で読める() {
        var folder=CreateDiskFolder();
        using var logDevice=Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var objDevice=Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        var (dict,store)=CreateDisk(logDevice,objDevice);
        using var _store=store;
        using var _dict=(IDisposable)dict;

        dict[1]="value";
        store.Store.Log.ShiftReadOnlyAddress(store.Store.Log.TailAddress,wait: true);

        Assert.True(dict.TryGetValue(1,out var actual));
        Assert.Equal("value",actual);
    }

    [Fact, Trait("Category","Unit")]
    public void TryGetValue_FlushAndEvict後でも読める() {
        var folder=CreateDiskFolder();
        using var logDevice=Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var objDevice=Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        var (dict,store)=CreateDisk(logDevice,objDevice);
        using var _store=store;
        using var _dict=(IDisposable)dict;

        dict[1]="value";
        store.Store.Log.FlushAndEvict(true);

        Assert.True(dict.TryGetValue(1,out var actual));
        Assert.Equal("value",actual);
    }

    [Fact, Trait("Category","Unit")]
    public void 未使用ハンドラは例外を送出する() {
        var functions=default(global::Seamless.SessionFunctions.ManagedRead);
        var key=default(ManagedSessionValue);
        var input=default(EqualityEmpty);
        var value=default(ManagedSessionValue);
        var oldValue=default(ManagedSessionValue);
        var newValue=default(ManagedSessionValue);
        var output=default(ManagedOutput);
        var rmwInfo=default(RMWInfo);
        var deleteInfo=default(DeleteInfo);
        var recordInfo=default(RecordInfo);
        var epochAccessor=default(DummyEpochAccessor);

        var copyUpdater=Assert.Throws<NotSupportedException>(() => {
            functions.CopyUpdater(ref key,ref input,ref oldValue,ref newValue,ref output,ref rmwInfo,ref recordInfo);
        });
        Assert.Equal("RMWで更新しない限りない",copyUpdater.Message);

        var postCopyUpdater=Assert.Throws<NotSupportedException>(() => {
            functions.PostCopyUpdater(ref key,ref input,ref oldValue,ref newValue,ref output,ref rmwInfo);
        });
        Assert.Equal("RMWで更新しない限りない",postCopyUpdater.Message);

        var modifiedLength=Assert.Throws<NotSupportedException>(() => {
            functions.GetRMWModifiedValueLength(ref value,ref input);
        });
        Assert.Equal("NeedCopyUpdate() が常に false 計上デッドコード",modifiedLength.Message);

        var singleDeleter=Assert.Throws<NotSupportedException>(() => {
            functions.SingleDeleter(ref key,ref value,ref deleteInfo,ref recordInfo);
        });
        Assert.Equal("ManagedReadはReadのみを使用する",singleDeleter.Message);

        var postSingleDeleter=Assert.Throws<NotSupportedException>(() => {
            functions.PostSingleDeleter(ref key,ref deleteInfo);
        });
        Assert.Equal("ManagedReadはReadのみを使用する",postSingleDeleter.Message);

        var concurrentDeleter=Assert.Throws<NotSupportedException>(() => {
            functions.ConcurrentDeleter(ref key,ref value,ref deleteInfo,ref recordInfo);
        });
        Assert.Equal("ManagedReadはReadのみを使用する",concurrentDeleter.Message);

        var postDeleteOperation=Assert.Throws<NotSupportedException>(() => {
            functions.PostDeleteOperation(ref key,ref deleteInfo,epochAccessor);
        });
        Assert.Equal("ManagedReadはReadのみを使用する",postDeleteOperation.Message);
    }
}