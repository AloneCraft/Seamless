using System.Reflection;

namespace Test.Benchmark;

public sealed class ベンチマーク表示名テスト {
    [Fact]
    public void 具体ベンチマーク型の直接基底型を表示名に使用する() {
        var アセンブリ=Assembly.Load(new AssemblyName("Benchmark.Optimization.All"));

        Assert.Equal("Array<int>",ベンチマークパスを取得(アセンブリ,"Benchmark.Optimization.Rewrite.ArrayInt32"));
        Assert.Equal("Array<ClassInt32>",ベンチマークパスを取得(アセンブリ,"Benchmark.Optimization.Rewrite.ArrayClassInt32"));
        Assert.Equal("IEnumerable<int>",ベンチマークパスを取得(アセンブリ,"Benchmark.Optimization.Rewrite.IEnumerableInt32"));
        Assert.Equal("List<int>",ベンチマークパスを取得(アセンブリ,"Benchmark.Optimization.Rewrite.ListInt32"));
        Assert.Equal("Range",ベンチマークパスを取得(アセンブリ,"Benchmark.Optimization.Rewrite.Range_start_count"));

        var ArrayInt32=アセンブリ.GetType("Benchmark.Optimization.Rewrite.ArrayInt32",throwOnError:true)
            ??throw new TypeLoadException("Benchmark.Optimization.Rewrite.ArrayInt32");
        var Where_predicate_Count=ArrayInt32.GetMethod("Where_predicate_Count")
            ??throw new MissingMethodException(ArrayInt32.FullName,"Where_predicate_Count");
        Assert.Equal("Array<int>.Where_predicate_Count",ベンチマークパスを取得(アセンブリ,ArrayInt32.FullName!)+"."+Where_predicate_Count.Name);
    }

    [Fact]
    public void PipelineFusionは演算子境界でネストした型構造の表示名を持つ() {
        var アセンブリ=Assembly.Load(new AssemblyName("Benchmark.Optimization.All"));

        // Persisted<>.Where_predicate.Select_selector のように、
        // Where_predicateネストクラス+Select_selectorメソッドという構造を全11型が共有する。
        最適化型を検証(
            アセンブリ,
            "PipelineFusion",
            ("ArrayInt32","Array<int>.Where_predicate","Select_selector"),
            ("ArrayClassInt32","Array<ClassInt32>.Where_predicate","Select_selector"),
            ("IEnumerableInt32","IEnumerable<int>.Where_predicate","Select_selector"),
            ("IEnumerableClassInt32","IEnumerable<ClassInt32>.Where_predicate","Select_selector"),
            ("ListInt32","List<int>.Where_predicate","Select_selector"),
            ("ListClassInt32","List<ClassInt32>.Where_predicate","Select_selector"),
            ("Range_start_count","Range.Where_predicate","Select_selector"),
            ("PersistedInt32","Persisted<int>.Where_predicate","Select_selector"),
            ("PersistedClassInt32","Persisted<ClassInt32>.Where_predicate","Select_selector"),
            ("TsavoriteInt32","Tsavorite<int>.Where_predicate","Select_selector"),
            ("TsavoriteClassInt32","Tsavorite<ClassInt32>.Where_predicate","Select_selector"));

        foreach(var 旧型名 in new[]{
            "ArrayPipeline`1","EnumerablePipeline`1","ListPipeline`1","RangePipeline","PersistedPipeline`1","TsavoritePipeline`1",
        }){
            Assert.Null(アセンブリ.GetType("Shared.Linq.Optimization."+旧型名,throwOnError:false));
        }
    }

    [Fact]
    public void PredicatePushdownはArrayとIEnumerableのみ演算子境界でネストする() {
        var アセンブリ=Assembly.Load(new AssemblyName("Benchmark.Optimization.All"));

        // Array<>.Select_selector.Where_predicate のようにネストされているのはArray/IEnumerableのみで、
        // List/Range/Persisted/Tsavoriteは Select_selector_Where_predicate という単一メソッド名のフラット構造のまま。
        最適化型を検証(
            アセンブリ,
            "PredicatePushdown",
            ("ArrayInt32","Array<int>.Select_selector","Where_predicate"),
            ("ArrayClassInt32","Array<ClassInt32>.Select_selector","Where_predicate"),
            ("IEnumerableInt32","IEnumerable<int>.Select_selector","Where_predicate"),
            ("IEnumerableClassInt32","IEnumerable<ClassInt32>.Select_selector","Where_predicate"),
            ("ListInt32","List<int>","Select_selector_Where_predicate"),
            ("ListClassInt32","List<ClassInt32>","Select_selector_Where_predicate"),
            ("Range_start_count","Range","Select_selector_Where_predicate"),
            ("PersistedInt32","Persisted<int>","Select_selector_Where_predicate"),
            ("PersistedClassInt32","Persisted<ClassInt32>","Select_selector_Where_predicate"),
            ("TsavoriteInt32","Tsavorite<int>","Select_selector_Where_predicate"),
            ("TsavoriteClassInt32","Tsavorite<ClassInt32>","Select_selector_Where_predicate"));

        foreach(var 旧型名 in new[]{
            "ArrayPushdown`1","EnumerablePushdown`1","ListPushdown`1","RangePushdown","PersistedPushdown`1","TsavoritePushdown`1",
        }){
            Assert.Null(アセンブリ.GetType("Shared.Linq.Optimization."+旧型名,throwOnError:false));
        }
    }

    [Fact]
    public void 基底型を取得できない場合は空のパスを返す() {
        var メソッド=typeof(global::UI.ベンチマークカタログ).GetMethod("ベンチマークパスを作成",BindingFlags.Static|BindingFlags.NonPublic)
            ??throw new MissingMethodException(nameof(global::UI.ベンチマークカタログ),"ベンチマークパスを作成");

        Assert.Equal(string.Empty,(string)メソッド.Invoke(null,[typeof(object)])!);
    }

    private static string ベンチマークパスを取得(Assembly アセンブリ,string 型完全名) {
        var 型=アセンブリ.GetType(型完全名,throwOnError:true)
            ??throw new TypeLoadException(型完全名);
        var メソッド=typeof(global::UI.ベンチマークカタログ).GetMethod("ベンチマークパスを作成",BindingFlags.Static|BindingFlags.NonPublic)
            ??throw new MissingMethodException(nameof(global::UI.ベンチマークカタログ),"ベンチマークパスを作成");
        return (string)メソッド.Invoke(null,[型])!;
    }

    private static void 最適化型を検証(
        Assembly アセンブリ,
        string 最適化名,
        params (string 具象型名,string ベンチマークパス,string メソッド名)[]期待一覧) {
        foreach(var 期待 in 期待一覧){
            var 型完全名="Benchmark.Optimization."+最適化名+"."+期待.具象型名;
            var 型=アセンブリ.GetType(型完全名,throwOnError:true)
                ??throw new TypeLoadException(型完全名);
            var 基底型=型.BaseType??throw new TypeLoadException(型完全名+"の基底型");
            Assert.Equal("Shared.Linq.Optimization."+最適化名,基底型.Namespace);
            Assert.Equal(期待.ベンチマークパス,ベンチマークパスを取得(アセンブリ,型完全名));
            Assert.Equal(
                期待.ベンチマークパス+"."+期待.メソッド名,
                完全表示名を取得(アセンブリ,型完全名,期待.メソッド名));
        }
    }

    private static string 完全表示名を取得(Assembly アセンブリ,string 型完全名,string メソッド名) {
        var 型=アセンブリ.GetType(型完全名,throwOnError:true)
            ??throw new TypeLoadException(型完全名);
        var メソッド=型.GetMethod(メソッド名)
            ??throw new MissingMethodException(型.FullName,メソッド名);
        return ベンチマークパスを取得(アセンブリ,型完全名)+"."+メソッド.Name;
    }
}
