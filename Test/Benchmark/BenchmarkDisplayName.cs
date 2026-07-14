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
    public void PipelineFusionとPredicatePushdownの型構造と表示名を統一する() {
        var アセンブリ=Assembly.Load(new AssemblyName("Benchmark.Optimization.All"));

        {
            // PipelineFusionの新しい型構造と表示名
            新しい最適化型を検証(
                アセンブリ,
                "PipelineFusion",
                "Where_predicate_Select_selector",
                ("ArrayInt32","Array<int>"),
                ("ArrayClassInt32","Array<ClassInt32>"),
                ("IEnumerableInt32","IEnumerable<int>"),
                ("IEnumerableClassInt32","IEnumerable<ClassInt32>"),
                ("ListInt32","List<int>"),
                ("ListClassInt32","List<ClassInt32>"),
                ("Range_start_count","Range"),
                ("PersistedInt32","Persisted<int>"),
                ("PersistedClassInt32","Persisted<ClassInt32>"),
                ("TsavoriteInt32","Tsavorite<int>"),
                ("TsavoriteClassInt32","Tsavorite<ClassInt32>"));
        }
        {
            // PredicatePushdownの新しい型構造と表示名
            新しい最適化型を検証(
                アセンブリ,
                "PredicatePushdown",
                "Select_selector_Where_predicate",
                ("ArrayInt32","Array<int>"),
                ("ArrayClassInt32","Array<ClassInt32>"),
                ("IEnumerableInt32","IEnumerable<int>"),
                ("IEnumerableClassInt32","IEnumerable<ClassInt32>"),
                ("ListInt32","List<int>"),
                ("ListClassInt32","List<ClassInt32>"),
                ("Range_start_count","Range"),
                ("PersistedInt32","Persisted<int>"),
                ("PersistedClassInt32","Persisted<ClassInt32>"),
                ("TsavoriteInt32","Tsavorite<int>"),
                ("TsavoriteClassInt32","Tsavorite<ClassInt32>"));
        }
        {
            // 旧公開型を残さない
            foreach(var 旧型名 in new[]{
                "ArrayPipeline`1","EnumerablePipeline`1","ListPipeline`1","RangePipeline","PersistedPipeline`1","TsavoritePipeline`1",
                "ArrayPushdown`1","EnumerablePushdown`1","ListPushdown`1","RangePushdown","PersistedPushdown`1","TsavoritePushdown`1",
            }){
                Assert.Null(アセンブリ.GetType("Seamless.Linq.Optimization."+旧型名,throwOnError:false));
            }
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

    private static void 新しい最適化型を検証(
        Assembly アセンブリ,
        string 最適化名,
        string メソッド名,
        params (string 具象型名,string ベンチマークパス)[]期待一覧) {
        foreach(var 期待 in 期待一覧){
            var 型完全名="Benchmark.Optimization."+最適化名+"."+期待.具象型名;
            var 型=アセンブリ.GetType(型完全名,throwOnError:true)
                ??throw new TypeLoadException(型完全名);
            var 基底型=型.BaseType??throw new TypeLoadException(型完全名+"の基底型");
            Assert.Equal("Seamless.Linq.Optimization."+最適化名,基底型.Namespace);
            Assert.Equal(期待.ベンチマークパス,ベンチマークパスを取得(アセンブリ,型完全名));
        }

        Assert.Equal(
            "Array<int>."+メソッド名,
            完全表示名を取得(アセンブリ,"Benchmark.Optimization."+最適化名+".ArrayInt32",メソッド名));
        Assert.Equal(
            "Array<ClassInt32>."+メソッド名,
            完全表示名を取得(アセンブリ,"Benchmark.Optimization."+最適化名+".ArrayClassInt32",メソッド名));
    }

    private static string 完全表示名を取得(Assembly アセンブリ,string 型完全名,string メソッド名) {
        var 型=アセンブリ.GetType(型完全名,throwOnError:true)
            ??throw new TypeLoadException(型完全名);
        var メソッド=型.GetMethod(メソッド名)
            ??throw new MissingMethodException(型.FullName,メソッド名);
        return ベンチマークパスを取得(アセンブリ,型完全名)+"."+メソッド.Name;
    }
}
