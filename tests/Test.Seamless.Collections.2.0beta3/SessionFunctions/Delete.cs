using Common;
using Garnet.common;
using MemoryPack;
using Seamless.Collections;
using Tsavorite.core;
namespace Test.Seamless.SessionFunctions;


public class Delete {
    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize = Const.MinObjectLogCapacity;
    private static FixedSpanByteKey MakeKey(int value) => (FixedSpanByteKey)(ReadOnlySpan<byte>)MemoryPackSerializer.Serialize(value);

    [Fact, Trait("Category","Coverage")]
    public void NeedInitialUpdate() {
        using var store = new DictionaryStore<int,string>();
        using var dict = store.Create();

        dict[1]="one";
        Assert.True(dict.Remove(1));
        Assert.False(dict.Remove(1));
    }

    [Fact, Trait("Category","Coverage")]public void NeedCopyUpdate() {
        var folder = CreateFolder();
        using var logDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var store = new DictionaryStore<int,string>(logDevice,objDevice,MemorySize: TestMemorySize,MutableFraction: 0);
        using var dict = store.Create();

        for(var a = 0; a<300; a++) {
            dict[a]=$"v{a}";
        }
        Assert.True(dict.Remove(0));
        Assert.False(dict.Remove(99999));
    }
    [Fact,Trait("Category","Coverage")]public void CopyUpdate()=>this.NeedCopyUpdate();

    [Fact, Trait("Category","Coverage")]public void ConvertOutputToHeap() {
        var folder = CreateFolder();
        using var LogDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var ObjDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var store = new DictionaryStore<int,string>(LogDevice,ObjDevice,MemorySize: Const.MinObjectLogCapacity);
        using var dict = store.Create();
        dict.Add(0,"target");
        // 全レコードをディスクに追い出す → 次のRMWでIsPending=true
        // DeleteSessionFunctions の RMW が Pending → ConvertOutputToHeap が間接的に呼ばれる
        store.Store.Log.FlushAndEvict(true);
        dict.Remove(0);
    }
    [Fact,Trait("Category","Coverage")]public void GetRMWModifiedFieldInfo_TSourceLogRecord_PinnedSpanByte()=>this.ConvertOutputToHeap();
    [Fact,Trait("Category","Coverage")]public void InPlaceUpdater_LogRecord_RecordSizeInfo_PinnedSpanByte_SpanByteAndMemory_RMWInfo()=>this.ConvertOutputToHeap();
    [Fact,Trait("Category","Coverage")]public void PostRMWOperation()=>this.ConvertOutputToHeap();
}
