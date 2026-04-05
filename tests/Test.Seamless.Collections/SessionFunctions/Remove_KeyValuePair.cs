using Common;
using Seamless.Collections;
using Tsavorite.core;
namespace Test.Seamless.SessionFunctions;


public class Remove_KeyValuePair {
    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize = Const.MinObjectLogCapacity;

    // ----------------------------------------------------------------
    // Remove_KeyValuePair
    // ----------------------------------------------------------------

    private void Remove() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict[1]="one";
        Assert.True(dict.Remove(new KeyValuePair<int,string>(1,"one")));
    }
    [Fact,Trait("Category",Unit)]public void InPlaceUpdater()=>this.Remove();
    [Fact,Trait("Category",Unit)]public void GetRMWModifiedFieldInfo()=>this.Remove();
    [Fact,Trait("Category",Unit)]public void PostRMWOperation()=>this.Remove();
/*
    [Fact,Trait("Category",Unit)]
    public void Remove_KeyValuePair_InPlaceUpdater_値不一致() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict[1]="one";
        Assert.False(dict.Remove(new KeyValuePair<int,string>(1,"wrong")));
    }
*/
    [Fact,Trait("Category",Unit)]
    public void NeedInitialUpdate() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        Assert.False(dict.Remove(new KeyValuePair<int,string>(999,"v")));
    }
    private void Remove_Disk() {
        var folder = CreateFolder();
        using var logDevice = Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice = Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var store = Store.Create<int,string>(MemorySize:TestMemorySize,MutableFraction:0,LogDevice:logDevice,ObjectLogDevice:objDevice);
        using var dict = store.Create();
        for(var a = 0; a<300; a++) {
            dict[a]=$"v{a}";
        }
        Assert.True(dict.Remove(new KeyValuePair<int,string>(0,"v0")));

    }
    [Fact,Trait("Category",Unit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void NeedCopyUpdate()=>this.Remove_Disk();
    [Fact,Trait("Category",Unit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void CopyUpdater()=>this.Remove_Disk();
    [Fact,Trait("Category",Unit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void RMWCompletionCallback()=>this.Remove_Disk();
    //[Fact,Trait("Category",Unit)]
    //[System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    //public void GetRMWModifiedFieldInfo_TSourceLogRecord_PinnedSpanByte()=>this.Remove_Disk();
/*
    [Fact,Trait("Category",Unit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void Remove_KeyValuePair_NeedCopyUpdate_値不一致() {
        var folder = CreateFolder();
        using var logDevice = Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice = Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var store = Store.Create<int,string>(logDevice,objDevice,MemorySize: TestMemorySize,MutableFraction: 0);
        using var dict = store.Create();
        for(var a = 0; a<300; a++) {
            dict[a]=$"v{a}";
        }
        Assert.False(dict.Remove(new KeyValuePair<int,string>(0,"wrong")));
    }

    // ── FlushAndEvict (ConvertOutputToHeap path) ──────────────────────
    // 1件書き込み後に FlushAndEvict → 次の Remove で IsPending=true
    // Tsavorite が Output をヒープに退避するために ConvertOutputToHeap が呼ばれる
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    private void RemoveKvp_FlushAndEvict() {
        var folder = CreateFolder();
        using var logDevice = Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice = Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var store = Store.Create<int,string>(logDevice,objDevice,MemorySize: TestMemorySize);
        using var dict = store.Create();
        dict[0]="target";
        // 全レコードをディスクに追い出す → 次の Remove で IsPending=true → ConvertOutputToHeap が呼ばれる
        store.Store.Log.FlushAndEvict(true);
        Assert.True(dict.Remove(new KeyValuePair<int,string>(0,"target")));
    }
    [Fact,Trait("Category",Unit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void Remove_KeyValuePair_ConvertOutputToHeap()=>this.RemoveKvp_FlushAndEvict();

    // ----------------------------------------------------------------
    // Remove_TKey_TValue
    // ----------------------------------------------------------------

    private void RemoveTKeyTValue_InPlaceUpdater() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict[1]="one";
        Assert.True(dict.Remove(1,out var value));
        Assert.Equal("one",value);
    }
    [Fact,Trait("Category",Unit)]public void Remove_TKey_TValue_InPlaceUpdater()=>this.RemoveTKeyTValue_InPlaceUpdater();
    [Fact,Trait("Category",Unit)]public void Remove_TKey_TValue_GetRMWModifiedFieldInfo()=>this.RemoveTKeyTValue_InPlaceUpdater();
    [Fact,Trait("Category",Unit)]public void Remove_TKey_TValue_PostRMWOperation()=>this.RemoveTKeyTValue_InPlaceUpdater();

    [Fact,Trait("Category",Unit)]
    public void Remove_TKey_TValue_NeedInitialUpdate_キー未存在() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        Assert.False(dict.Remove(999,out _));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    private void RemoveTKeyTValue_CopyUpdater() {
        var folder = CreateFolder();
        using var logDevice = Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice = Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var store = Store.Create<int,string>(logDevice,objDevice,MemorySize: TestMemorySize,MutableFraction: 0);
        using var dict = store.Create();
        for(var a = 0; a<300; a++) {
            dict[a]=$"v{a}";
        }
        Assert.True(dict.Remove(0,out var value));
        Assert.Equal("v0",value);
    }
    [Fact,Trait("Category",Unit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void Remove_TKey_TValue_CopyUpdater()=>this.RemoveTKeyTValue_CopyUpdater();
    [Fact,Trait("Category",Unit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void Remove_TKey_TValue_NeedCopyUpdate()=>this.RemoveTKeyTValue_CopyUpdater();
    [Fact,Trait("Category",Unit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void Remove_TKey_TValue_RMWCompletionCallback()=>this.RemoveTKeyTValue_CopyUpdater();
    [Fact,Trait("Category",Unit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void Remove_TKey_TValue_GetRMWModifiedFieldInfo_TSourceLogRecord_PinnedSpanByte()=>this.RemoveTKeyTValue_CopyUpdater();
*/
    // ── FlushAndEvict (ConvertOutputToHeap path) ──────────────────────
    // 1件書き込み後に FlushAndEvict → 次の Remove で IsPending=true
    // Tsavorite が Output をヒープに退避するために ConvertOutputToHeap が呼ばれる
    [Fact,Trait("Category",Unit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void Remove_TKey_TValue_ConvertOutputToHeap(){
        var folder = CreateFolder();
        using var logDevice = Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice = Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var store = Store.Create<int,string>(MemorySize:TestMemorySize,LogDevice:logDevice,ObjectLogDevice:objDevice);
        using var dict = store.Create();
        dict[0]="target";
        // 全レコードをディスクに追い出す → 次の Remove で IsPending=true → ConvertOutputToHeap が呼ばれる
        store.Store.Log.FlushAndEvict(true);
        Assert.True(dict.Remove(new KeyValuePair<int,string>(0,"target")));
    }
}
