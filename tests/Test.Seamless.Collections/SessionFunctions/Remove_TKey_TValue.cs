using Common;
using Seamless.Collections;
using Tsavorite.core;
namespace Test.Seamless.SessionFunctions;


public class Remove_TKey_TValue {
    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize = Const.MinObjectLogCapacity;

    // ── In-memory (Mutable key) ──────────────────────────────────────
    // dict[1]="one" → Remove(1, out value) → true, value="one"
    // キー存在(Mutable) → RMW → GetRMWModifiedFieldInfo → InPlaceUpdater(値コピー+ExpireAndStop) → PostRMWOperation
    private void Remove() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict[1]="one";
        Assert.True(dict.Remove(1,out var value));
        Assert.Equal("one",value);
    }
    [Fact,Trait("Category",Unit)]public void InPlaceUpdater()=>this.Remove();
    [Fact,Trait("Category",Unit)]public void GetRMWModifiedFieldInfo()=>this.Remove();
    [Fact,Trait("Category",Unit)]public void PostRMWOperation()=>this.Remove();
    // キー未存在 → NeedInitialUpdate → false → NotFound
    [Fact,Trait("Category",Unit)]
    public void NeedInitialUpdate() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        Assert.False(dict.Remove(999,out _));
    }


    // ── Disk (IsPending path: NeedCopyUpdate → CopyUpdater → RMWCompletionCallback) ─
    // MutableFraction:0 で全レコードをディスク領域に追い出し
    // Remove → NeedCopyUpdate → CopyUpdater(値コピー+ExpireAndStop) → PostRMWOperation
    // ディスク読み込み時は RMWCompletionCallback / GetRMWModifiedFieldInfo(TSourceLogRecord) も通る
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    private void Remove_Disk() {
        var folder = CreateFolder();
        using var logDevice = Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice = Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var store = Store.Create<int,string>(MemorySize:TestMemorySize,MutableFraction:0,LogDevice:logDevice,ObjectLogDevice:objDevice);
        using var dict = store.Create();
        for(var a = 0; a<300; a++) {
            dict[a]=$"v{a}";
        }
        Assert.True(dict.Remove(0,out var value));
        Assert.Equal("v0",value);
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
    [Fact,Trait("Category",Unit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void GetRMWModifiedFieldInfo_TSourceLogRecord_PinnedSpanByte()=>this.Remove_Disk();

    // ── FlushAndEvict (ConvertOutputToHeap path) ──────────────────────
    // 1件書き込み後に FlushAndEvict → 次の Remove で IsPending=true
    // Tsavorite が Output をヒープに退避するために ConvertOutputToHeap が呼ばれる
    [Fact,Trait("Category",Unit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void ConvertOutputToHeap(){
        var folder = CreateFolder();
        using var logDevice = Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice = Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var store = Store.Create<int,string>(MemorySize:TestMemorySize,LogDevice:logDevice,ObjectLogDevice:objDevice);
        using var dict = store.Create();
        dict[0]="target";
        // 全レコードをディスクに追い出す → 次の Remove で IsPending=true → ConvertOutputToHeap が呼ばれる
        store.Store.Log.FlushAndEvict(true);
        Assert.True(dict.Remove(0,out var value));
        Assert.Equal("target",value);
    }
}
