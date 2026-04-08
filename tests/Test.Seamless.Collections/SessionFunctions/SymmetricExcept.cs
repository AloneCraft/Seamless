using Seamless;
using Seamless.Store;
using Tsavorite.core;
namespace Test.Seamless.SessionFunctions;


public class SymmetricExcept {
    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize = Const.MinObjectLogCapacity;

    // ── In-memory (Mutable key) ──────────────────────────────────────
    // setA={1,2,3}, setB={2,3,4} → SymmetricExceptWith → setA={1,4}
    // key 2,3 は両方にある → RMW → GetRMWModifiedFieldInfo → InPlaceUpdater(ExpireAndStop) → PostRMWOperation
    // key 4 は setA に無い → RMW → NeedInitialUpdate → InitialUpdater → PostInitialUpdater → PostRMWOperation
    private void SymmetricExceptWith() {
        using var storeA = Factory.Create<int>();
        using var storeB = Factory.Create<int>();
        using var setA = storeA.Create();
        using var setB = storeB.Create();
        setA.Add(1);
        setA.Add(2);
        setA.Add(3);
        setB.Add(2);
        setB.Add(3);
        setB.Add(4);

        setA.SymmetricExceptWith(setB);

        Assert.Contains(1,setA);
        Assert.DoesNotContain(2,setA);
        Assert.DoesNotContain(3,setA);
        Assert.Contains(4,setA);
    }
    [Fact,Trait("Category",Unit)]public void InPlaceUpdater()=>this.SymmetricExceptWith();
    [Fact,Trait("Category",Unit)]public void GetRMWModifiedFieldInfo()=>this.SymmetricExceptWith();
    [Fact,Trait("Category",Unit)]public void PostRMWOperation()=>this.SymmetricExceptWith();
    [Fact,Trait("Category",Unit)]public void NeedInitialUpdate()=>this.SymmetricExceptWith();
    [Fact,Trait("Category",Unit)]public void GetRMWInitialFieldInfo()=>this.SymmetricExceptWith();
    [Fact,Trait("Category",Unit)]public void InitialUpdater()=>this.SymmetricExceptWith();
    [Fact,Trait("Category",Unit)]public void PostInitialUpdater()=>this.SymmetricExceptWith();

    // ── ReadOnly in-memory (ShiftReadOnlyAddress でログ全体を ReadOnly に) ─
    // ShiftReadOnlyAddress(TailAddress, wait:true) で全レコードを ReadOnly 領域に移動してから
    // SymmetricExceptWith を呼ぶと key 2,3 → NeedCopyUpdate → CopyUpdater(ExpireAndStop) が通る
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    private void SymmetricExceptWith_ReadOnly() {
        var folder = CreateFolder();
        using var logDevice = Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice = Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var storeA = Factory.Create<int>(logDevice,objDevice,MemorySize:TestMemorySize);
        using var storeB = Factory.Create<int>();
        using var setA = storeA.Create();
        using var setB = storeB.Create();
        setA.Add(1);
        setA.Add(2);
        setA.Add(3);
        setB.Add(2);
        setB.Add(3);
        setB.Add(4);

        // 全レコードを ReadOnly 領域に移動 → 次の RMW で NeedCopyUpdate → CopyUpdater が呼ばれる
        storeA.Store.Log.ShiftReadOnlyAddress(storeA.Store.Log.TailAddress,true);

        setA.SymmetricExceptWith(setB);

        Assert.Contains(1,setA);
        Assert.DoesNotContain(2,setA);
        Assert.DoesNotContain(3,setA);
        Assert.Contains(4,setA);
    }
    [Fact,Trait("Category",Unit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void NeedCopyUpdate()=>this.SymmetricExceptWith_ReadOnly();
    [Fact,Trait("Category",Unit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void CopyUpdater()=>this.SymmetricExceptWith_ReadOnly();

    // ── Disk (IsPending path: 両ストアともディスク) ─────────────────
    [Fact,Trait("Category",Unit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void IsPending経路でも動作する() {
        var folderA = CreateFolder();
        var folderB = CreateFolder();
        using var logDeviceA = Devices.CreateLogDevice(Path.Combine(folderA,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDeviceA = Devices.CreateLogDevice(Path.Combine(folderA+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var logDeviceB = Devices.CreateLogDevice(Path.Combine(folderB,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDeviceB = Devices.CreateLogDevice(Path.Combine(folderB+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var storeA = Factory.Create<int>(logDeviceA,objDeviceA,MemorySize:TestMemorySize,MutableFraction:0);
        using var storeB = Factory.Create<int>(logDeviceB,objDeviceB,MemorySize:TestMemorySize,MutableFraction:0);
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

    // ── FlushAndEvict (ConvertOutputToHeap path) ──────────────────────
    // 1件書き込み後に FlushAndEvict → 次の SymmetricExceptWith で IsPending=true
    // Tsavorite が Output をヒープに退避するために ConvertOutputToHeap が呼ばれる
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    private void SymmetricExceptWith_FlushAndEvict() {
        var folder = CreateFolder();
        using var logDevice = Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice = Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var storeA = Factory.Create<int>(logDevice,objDevice,MemorySize:TestMemorySize);
        using var storeB = Factory.Create<int>();
        using var setA = storeA.Create();
        using var setB = storeB.Create();
        setA.Add(1);
        setA.Add(2);
        setA.Add(3);
        // setB は setA のサブセット ({2,3}) にする:
        // key 2,3 は setA ディスク上に存在 → IsPending → ConvertOutputToHeap → CopyUpdater(ExpireAndStop) → RMWCompletionCallback
        // key 4 のような setA 未存在キーを含めると NeedInitialUpdate IsPending も発生するが同じく RMWCompletionCallback が呼ばれる
        setB.Add(2);
        setB.Add(3);
        // 全レコードをディスクに追い出す → 次の SymmetricExceptWith で IsPending=true → ConvertOutputToHeap が呼ばれる
        storeA.Store.Log.FlushAndEvict(true);
        setA.SymmetricExceptWith(setB);
        Assert.Contains(1,setA);
        Assert.DoesNotContain(2,setA);
        Assert.DoesNotContain(3,setA);
    }
    [Fact,Trait("Category",Unit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void RMWCompletionCallback()=>this.SymmetricExceptWith_FlushAndEvict();
    [Fact,Trait("Category",Unit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void ConvertOutputToHeap()=>this.SymmetricExceptWith_FlushAndEvict();
}
