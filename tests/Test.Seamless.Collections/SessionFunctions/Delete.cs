using Common;
using Garnet.common;
using MemoryPack;
using Seamless.Collections;
using Tsavorite.core;
namespace Test.Seamless.SessionFunctions;


public class Delete {
    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize = Const.MinObjectLogCapacity;
#if TSAVORITE2_0BETA3
    private static FixedSpanByteKey MakeKey(int value) => (FixedSpanByteKey)(ReadOnlySpan<byte>)MemoryPackSerializer.Serialize(value);
#elif TSAVORITE1_1_1
    private static SpanByte MakeKey(int value) => SpanByte.FromPinnedSpan(MemoryPackSerializer.Serialize(value));
#endif

    [Fact, Trait("Category",Unit)]
    public void NeedInitialUpdate() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();

        dict[1]="one";
        Assert.True(dict.Remove(1));
        Assert.False(dict.Remove(1));
    }

    [Fact, Trait("Category",Unit)]public void NeedCopyUpdate() {
        var folder = CreateFolder();
        using var logDevice = Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice = Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var store = Store.Create<int,string>(MemorySize:TestMemorySize,MutableFraction:0,LogDevice:logDevice,ObjectLogDevice:objDevice);
        using var dict = store.Create();

        for(var a = 0; a<300; a++) {
            dict[a]=$"v{a}";
        }
        Assert.True(dict.Remove(0));
        Assert.False(dict.Remove(99999));
    }
    [Fact,Trait("Category",Unit)]public void CopyUpdate()=>this.NeedCopyUpdate();

    [Fact, Trait("Category",Unit)]public void ConvertOutputToHeap() {
        var folder = CreateFolder();
        using var LogDevice = Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var ObjDevice = Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var store = Store.Create<int,string>(MemorySize:Const.MinObjectLogCapacity,LogDevice:LogDevice,ObjectLogDevice:ObjDevice);
        using var dict = store.Create();
        dict.Add(0,"target");
        // 全レコードをディスクに追い出す → 次のRMWでIsPending=true
        // DeleteSessionFunctions の RMW が Pending → ConvertOutputToHeap が間接的に呼ばれる
        store.Store.Log.FlushAndEvict(true);
        dict.Remove(0);
    }
    [Fact,Trait("Category",Unit)]public void GetRMWModifiedFieldInfo_TSourceLogRecord_PinnedSpanByte()=>this.ConvertOutputToHeap();
    [Fact,Trait("Category",Unit)]public void InPlaceUpdater_LogRecord_RecordSizeInfo_PinnedSpanByte_SpanByteAndMemory_RMWInfo()=>this.ConvertOutputToHeap();
    [Fact,Trait("Category",Unit)]public void PostRMWOperation()=>this.ConvertOutputToHeap();
}
