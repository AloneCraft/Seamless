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
}
