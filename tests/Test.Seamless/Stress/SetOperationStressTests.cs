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
    private const int StressSetOpCount = 256;

    private static string CreateTempDir(string tag) {
        var dir = Path.Combine(Environment.CurrentDirectory,nameof(SetOperationStressTests));
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
        int repeat,
        Action<UnmanagedUnmanagedHashSet<int>,UnmanagedUnmanagedHashSet<int>> op) {
        var temp = CreateTempDir("uintset_"+opName);
        try {
            var keysA = StressScript.SampleKeys(SeedA,count*4,count);
            var keysB = StressScript.SampleKeys(SeedB,count*4,count);
            var logA = Path.Combine(temp,"logA");
            var objA = Path.Combine(temp,"objA");
            var logB = Path.Combine(temp,"logB");
            var objB = Path.Combine(temp,"objB");
            var probeKeys = new HashSet<int>(keysA);
            probeKeys.UnionWith(keysB);
            using(var logDevA = Devices.CreateLogDevice(logA))
            using(var objDevA = Devices.CreateLogDevice(objA))
            using(var logDevB = Devices.CreateLogDevice(logB))
            using(var objDevB = Devices.CreateLogDevice(objB))
            using(var storeA = Factory.Create().Create<int>(logDevA,objDevA))
            using(var storeB = Factory.Create().Create<int>(logDevB,objDevB))
            using(var setA = storeA.Create())
            using(var setB = storeB.Create()) {
                try {
                    for(var r = 0; r<repeat; r++) {
                        setA.Clear();
                        setB.Clear();
                        foreach(var k in keysA) setA.Add(k);
                        foreach(var k in keysB) setB.Add(k);
                        storeA.Store.Log.FlushAndEvict(wait: true);
                        storeB.Store.Log.FlushAndEvict(wait: true);
                        op(setA,setB);
                        var expected = new HashSet<int>(keysA);
                        expected.UnionWith(keysB);
                        switch(opName) {
                            case "IntersectWith":
                                expected=new HashSet<int>(keysA);
                                expected.IntersectWith(keysB);
                                break;
                            case "SymmetricExceptWith":
                                expected=new HashSet<int>(keysA);
                                expected.SymmetricExceptWith(keysB);
                                break;
                            case "UnionWith":
                                expected=new HashSet<int>(keysA);
                                expected.UnionWith(keysB);
                                break;
                            case "ExceptWith":
                                expected=new HashSet<int>(keysA);
                                expected.ExceptWith(keysB);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException(nameof(opName),opName,null);
                        }
                        StressReferenceAssert.SetEquals(probeKeys,expected,key => setA.Contains(key));
                    }
                }
                finally {
                    storeA.Store.Log.FlushAndEvict(wait: true);
                    storeB.Store.Log.FlushAndEvict(wait: true);
                }
            }
        }
        finally {
            TryCleanup(temp);
        }
    }

    [Theory, InlineData(StressSetOpCount)]
    public void IntersectWith_UnmanagedInt(int count) {
        const int Count = 1;
        RunUnmanagedIntSetOp("IntersectWith",count,StressScale.Apply(Count),(a,b) => a.IntersectWith(b));
    }
    [Theory, InlineData(StressSetOpCount)]
    public void SymmetricExceptWith_UnmanagedInt(int count) {
        const int Count = 1;
        RunUnmanagedIntSetOp("SymmetricExceptWith",count,StressScale.Apply(Count),(a,b) => a.SymmetricExceptWith(b));
    }
    [Theory, InlineData(StressSetOpCount)]
    public void UnionWith_UnmanagedInt(int count) {
        const int Count = 1;
        RunUnmanagedIntSetOp("UnionWith",count,StressScale.Apply(Count),(a,b) => a.UnionWith(b));
    }
    [Theory, InlineData(StressSetOpCount)]
    public void ExceptWith_UnmanagedInt(int count) {
        const int Count = 1;
        RunUnmanagedIntSetOp("ExceptWith",count,StressScale.Apply(Count),(a,b) => a.ExceptWith(b));
    }

    private static void RunManagedStringSetOp(
        string opName,
        int count,
        int repeat,
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
            var probeKeys = new HashSet<string>(keysA,StringComparer.Ordinal);
            probeKeys.UnionWith(keysB);
            using(var logDevA = Devices.CreateLogDevice(logA))
            using(var objDevA = Devices.CreateLogDevice(objA))
            using(var logDevB = Devices.CreateLogDevice(logB))
            using(var objDevB = Devices.CreateLogDevice(objB))
            using(var storeA = Factory.Create().Create<string>(logDevA,objDevA,new ManagedTag()))
            using(var storeB = Factory.Create().Create<string>(logDevB,objDevB,new ManagedTag()))
            using(var setA = storeA.Create())
            using(var setB = storeB.Create()) {
                try {
                    for(var r = 0; r<repeat; r++) {
                        setA.Clear();
                        setB.Clear();
                        foreach(var k in keysA) setA.Add(k);
                        foreach(var k in keysB) setB.Add(k);
                        storeA.Store.Log.FlushAndEvict(wait: true);
                        storeB.Store.Log.FlushAndEvict(wait: true);
                        op(setA,setB);
                        var expected = new HashSet<string>(keysA,StringComparer.Ordinal);
                        switch(opName) {
                            case "IntersectWith":
                                expected.IntersectWith(keysB);
                                break;
                            case "SymmetricExceptWith":
                                expected.SymmetricExceptWith(keysB);
                                break;
                            case "UnionWith":
                                expected.UnionWith(keysB);
                                break;
                            case "ExceptWith":
                                expected.ExceptWith(keysB);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException(nameof(opName),opName,null);
                        }
                        StressReferenceAssert.SetEquals(probeKeys,expected,key => setA.Contains(key));
                    }
                }
                finally {
                    storeA.Store.Log.FlushAndEvict(wait: true);
                    storeB.Store.Log.FlushAndEvict(wait: true);
                }
            }
        }
        finally {
            TryCleanup(temp);
        }
    }

    [Theory, InlineData(StressSetOpCount)]
    public void IntersectWith_ManagedString(int count) {
        const int Count = 2;
        RunManagedStringSetOp("IntersectWith",count,StressScale.Apply(Count),(a,b) => a.IntersectWith(b));
    }
    [Theory, InlineData(StressSetOpCount)]
    public void SymmetricExceptWith_ManagedString(int count) {
        const int Count = 1;
        RunManagedStringSetOp("SymmetricExceptWith",count,StressScale.Apply(Count),(a,b) => a.SymmetricExceptWith(b));
    }
    [Theory, InlineData(StressSetOpCount)]
    public void UnionWith_ManagedString(int count) {
        const int Count = 2;
        RunManagedStringSetOp("UnionWith",count,StressScale.Apply(Count),(a,b) => a.UnionWith(b));
    }
    [Theory, InlineData(StressSetOpCount)]
    public void ExceptWith_ManagedString(int count) {
        const int Count = 2;
        RunManagedStringSetOp("ExceptWith",count,StressScale.Apply(Count),(a,b) => a.ExceptWith(b));
    }
}
