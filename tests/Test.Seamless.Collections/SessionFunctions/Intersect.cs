using Common;
using Seamless.Collections;

using Tsavorite.core;
namespace Test.Seamless.SessionFunctions;


public class Intersect {
    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize = Const.MinObjectLogCapacity;

    // ── In-memory (Mutable key) ──────────────────────────────────────
    // setA={1,2,3}, setB={2,3,4} → IntersectWith → setA={2,3}
    // key 1 は setB に無い → RMW → GetRMWModifiedFieldInfo → InPlaceUpdater(ExpireAndStop) → PostRMWOperation
    private void IntersectWith() {
        using var storeA = Store.Create<int>();
        using var storeB = Store.Create<int>();
        using var setA = storeA.Create();
        using var setB = storeB.Create();
        setA.Add(1);
        setA.Add(2);
        setA.Add(3);
        setB.Add(2);
        setB.Add(3);
        setB.Add(4);

        setA.IntersectWith(setB);

        Assert.DoesNotContain(1,setA);
        Assert.Contains(2,setA);
        Assert.Contains(3,setA);
        Assert.DoesNotContain(4,setA);
    }
    [Fact,Trait("Category","Coverage")]public void InPlaceUpdater()=>this.IntersectWith();
    [Fact,Trait("Category","Coverage")]public void GetRMWModifiedFieldInfo()=>this.IntersectWith();
    [Fact,Trait("Category","Coverage")]public void PostRMWOperation()=>this.IntersectWith();

    // ── ReadOnly in-memory (ShiftReadOnlyAddress でログ全体を ReadOnly に) ─
    // ShiftReadOnlyAddress(TailAddress, wait:true) で全レコードを ReadOnly 領域に移動してから
    // IntersectWith を呼ぶと RMW → NeedCopyUpdate → CopyUpdater(ExpireAndStop) が通る
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    private void IntersectWith_ReadOnly() {
        var folder = CreateFolder();
        using var logDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var storeA = Store.Create<int>(logDevice,objDevice,MemorySize: TestMemorySize);
        using var storeB = Store.Create<int>();
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

        setA.IntersectWith(setB);

        Assert.DoesNotContain(1,setA);
        Assert.Contains(2,setA);
        Assert.Contains(3,setA);
        Assert.DoesNotContain(4,setA);
    }
    [Fact,Trait("Category","Coverage")]public void NeedCopyUpdate()=>this.IntersectWith_ReadOnly();
    [Fact,Trait("Category","Coverage")]public void CopyUpdater()=>this.IntersectWith_ReadOnly();

    // ── Disk (IsPending path: 両ストアともディスク) ─────────────────
    [Fact,Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void IsPending経路でも動作する() {
        var folderA = CreateFolder();
        var folderB = CreateFolder();
        using var logDeviceA = new ManagedLocalStorageDevice(Path.Combine(folderA,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDeviceA = new ManagedLocalStorageDevice(Path.Combine(folderA+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var logDeviceB = new ManagedLocalStorageDevice(Path.Combine(folderB,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var objDeviceB = new ManagedLocalStorageDevice(Path.Combine(folderB+"_obj","obj.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var storeA = Store.Create<int>(logDeviceA,objDeviceA,MemorySize: TestMemorySize,MutableFraction: 0);
        using var storeB = Store.Create<int>(logDeviceB,objDeviceB,MemorySize: TestMemorySize,MutableFraction: 0);
        using var setA = storeA.Create();
        using var setB = storeB.Create();

        for(var a = 0; a<300; a++) {
            setA.Add(a);
            if(a%2==0) {
                setB.Add(a);
            }
        }

        setA.IntersectWith(setB);

        Assert.Contains(100,setA);
        Assert.DoesNotContain(101,setA);
    }
}
