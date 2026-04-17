using Seamless.Collections;
using Seamless.Stores;
using Tsavorite.core;
namespace Test.Seamless.Stress;

/// <summary>
/// HashSet の集合演算 (IntersectWith / SymmetricExceptWith / UnionWith / ExceptWith)
/// の決定論的 Stress テスト。シード違いで 2 セットを構築し演算結果をゴールデン比較する。
/// </summary>
[Trait("Category","Stress")]
public class SetOperationStressTests {
    private const int SeedA = 20260417;
    private const int SeedB = 20260419;

    private static string CreateTempDir(string tag) {
        var dir = Path.Combine(Path.GetTempPath(),"Seamless_SetStress_"+tag+"_"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
    private static void TryCleanup(string dir) {
        try { if(Directory.Exists(dir)) Directory.Delete(dir,recursive: true); }
        catch { }
    }

    private static void RunUnmanagedIntSetOp(
        string opName,
        int count,
        Action<UnmanagedUnmanagedHashSet<int>,UnmanagedUnmanagedHashSet<int>> op) {
        var temp = CreateTempDir("uintset_"+opName);
        try {
            var keysA = StressScript.SampleKeys(SeedA,count*4,count);
            var keysB = StressScript.SampleKeys(SeedB,count*4,count);
            var logA = Path.Combine(temp,"logA");
            var objA = Path.Combine(temp,"objA");
            var logB = Path.Combine(temp,"logB");
            var objB = Path.Combine(temp,"objB");
            string hash;
            using(var logDevA = Devices.CreateLogDevice(logA))
            using(var objDevA = Devices.CreateLogDevice(objA))
            using(var logDevB = Devices.CreateLogDevice(logB))
            using(var objDevB = Devices.CreateLogDevice(objB))
            using(var storeA = Factory.Create().Create<int>(logDevA,objDevA))
            using(var storeB = Factory.Create().Create<int>(logDevB,objDevB))
            using(var setA = storeA.Create())
            using(var setB = storeB.Create()) {
                foreach(var k in keysA) setA.Add(k);
                foreach(var k in keysB) setB.Add(k);
                storeA.Store.Log.FlushAndEvict(wait: true);
                storeB.Store.Log.FlushAndEvict(wait: true);
                op(setA,setB);
                hash=StressGolden.HashInt32Keys(setA);
            }
            StressGolden.AssertGolden($"SetOp_UintSet_{opName}_{count}",hash);
        }
        finally {
            TryCleanup(temp);
        }
    }

    [Theory, InlineData(10_000)]
    public void IntersectWith_UnmanagedInt(int count) =>
        RunUnmanagedIntSetOp("IntersectWith",count,(a,b) => a.IntersectWith(b));
    [Theory, InlineData(10_000)]
    public void SymmetricExceptWith_UnmanagedInt(int count) =>
        RunUnmanagedIntSetOp("SymmetricExceptWith",count,(a,b) => a.SymmetricExceptWith(b));
    [Theory, InlineData(10_000)]
    public void UnionWith_UnmanagedInt(int count) =>
        RunUnmanagedIntSetOp("UnionWith",count,(a,b) => a.UnionWith(b));
    [Theory, InlineData(10_000)]
    public void ExceptWith_UnmanagedInt(int count) =>
        RunUnmanagedIntSetOp("ExceptWith",count,(a,b) => a.ExceptWith(b));

    private static void RunManagedStringSetOp(
        string opName,
        int count,
        Action<ManagedHashSet<string>,ManagedHashSet<string>> op) {
        var temp = CreateTempDir("strset_"+opName);
        try {
            var rawA = StressScript.SampleKeys(SeedA,count*4,count);
            var rawB = StressScript.SampleKeys(SeedB,count*4,count);
            var keysA = rawA.Select(k => "s"+k).ToArray();
            var keysB = rawB.Select(k => "s"+k).ToArray();
            var logA = Path.Combine(temp,"logA");
            var objA = Path.Combine(temp,"objA");
            var logB = Path.Combine(temp,"logB");
            var objB = Path.Combine(temp,"objB");
            string hash;
            using(var logDevA = Devices.CreateLogDevice(logA))
            using(var objDevA = Devices.CreateLogDevice(objA))
            using(var logDevB = Devices.CreateLogDevice(logB))
            using(var objDevB = Devices.CreateLogDevice(objB))
            using(var storeA = Factory.Create().Create<string>(logDevA,objDevA,new ManagedTag()))
            using(var storeB = Factory.Create().Create<string>(logDevB,objDevB,new ManagedTag()))
            using(var setA = storeA.Create())
            using(var setB = storeB.Create()) {
                foreach(var k in keysA) setA.Add(k);
                foreach(var k in keysB) setB.Add(k);
                storeA.Store.Log.FlushAndEvict(wait: true);
                storeB.Store.Log.FlushAndEvict(wait: true);
                op(setA,setB);
                hash=StressGolden.HashStringKeys(setA);
            }
            StressGolden.AssertGolden($"SetOp_StrSet_{opName}_{count}",hash);
        }
        finally {
            TryCleanup(temp);
        }
    }

    [Theory, InlineData(10_000)]
    public void IntersectWith_ManagedString(int count) =>
        RunManagedStringSetOp("IntersectWith",count,(a,b) => a.IntersectWith(b));
    [Theory, InlineData(10_000)]
    public void SymmetricExceptWith_ManagedString(int count) =>
        RunManagedStringSetOp("SymmetricExceptWith",count,(a,b) => a.SymmetricExceptWith(b));
    [Theory, InlineData(10_000)]
    public void UnionWith_ManagedString(int count) =>
        RunManagedStringSetOp("UnionWith",count,(a,b) => a.UnionWith(b));
    [Theory, InlineData(10_000)]
    public void ExceptWith_ManagedString(int count) =>
        RunManagedStringSetOp("ExceptWith",count,(a,b) => a.ExceptWith(b));
}
