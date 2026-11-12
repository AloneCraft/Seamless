using System.Reflection;

namespace Test.Benchmark;

[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class ベンチマークカタログ引数 {
    [Fact]
    public void 仕様確認01() {
        var メソッド = メソッドを読み込む("Benchmark.Scalable.Tsavorite.Collections","Shared.Scalable.Tsavorite.Collections.Set.SetInt32","Add");
        var 引数一覧 = 引数集合一覧を取得(メソッド).First();

        Assert.Equal(
            ["Count","Index","Page","Segment","Memory","Fraction"],
            引数一覧.Select(static 引数 => 引数.名前).ToArray());
        Assert.Equal(
            ["65536","64","4096","4096","8388608","1"],
            引数一覧.Select(static 引数 => 引数.値).ToArray());
    }

    [Fact]
    public void 仕様確認02() {
        var メソッド = メソッドを読み込む("Benchmark.Scalable.Persisted.Collections","Shared.Scalable.Persisted.Collections.Set.SetInt32","Add");
        var 引数一覧 = 引数集合一覧を取得(メソッド).First();

        Assert.Equal(
            ["Count","Initial","Data"],
            引数一覧.Select(static 引数 => 引数.名前).ToArray());
        Assert.Equal(
            ["65536","1","4096"],
            引数一覧.Select(static 引数 => 引数.値).ToArray());
    }

    [Fact]
    public void 仕様確認03() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.スケーラブルTsavoriteセット);
        var 引数一覧 = 引数集合一覧を取得(メソッドを読み込む("Benchmark.Scalable.Tsavorite.Collections","Shared.Scalable.Tsavorite.Collections.Set.SetInt32","Add")).First();
        var 行一覧 = global::UI.ベンチマーク比較フォーマッター.比較行を構築(
            [
                報告行を作成(ソースタブ.左プロジェクト,"Add","Shared.Scalable.Tsavorite.Collections.Set.SetInt32",引数一覧),
            ],
            ソースタブ);

        var 行 = Assert.Single(行一覧);

        Assert.Equal("65536",global::UI.ベンチマーク引数フォーマッター.値を取得(行.引数一覧,"Count"));
        Assert.Equal("64",global::UI.ベンチマーク引数フォーマッター.値を取得(行.引数一覧,"Index"));
        Assert.Equal("4096",global::UI.ベンチマーク引数フォーマッター.値を取得(行.引数一覧,"Page"));
        Assert.Equal("4096",global::UI.ベンチマーク引数フォーマッター.値を取得(行.引数一覧,"Segment"));
        Assert.Equal("8388608",global::UI.ベンチマーク引数フォーマッター.値を取得(行.引数一覧,"Memory"));
        Assert.Equal("1",global::UI.ベンチマーク引数フォーマッター.値を取得(行.引数一覧,"Fraction"));
    }

    [Fact]
    public void 仕様確認04() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.スケーラブル永続化セット);
        var 引数一覧 = 引数集合一覧を取得(メソッドを読み込む("Benchmark.Scalable.Persisted.Collections","Shared.Scalable.Persisted.Collections.Set.SetInt32","Add")).First();
        var 行一覧 = global::UI.ベンチマーク比較フォーマッター.比較行を構築(
            [
                報告行を作成(ソースタブ.左プロジェクト,"Add","Shared.Scalable.Persisted.Collections.Set.SetInt32",引数一覧),
            ],
            ソースタブ);

        var 行 = Assert.Single(行一覧);

        Assert.Equal("65536",global::UI.ベンチマーク引数フォーマッター.値を取得(行.引数一覧,"Count"));
        Assert.Equal("1",global::UI.ベンチマーク引数フォーマッター.値を取得(行.引数一覧,"Initial"));
        Assert.Equal("4096",global::UI.ベンチマーク引数フォーマッター.値を取得(行.引数一覧,"Data"));
        Assert.DoesNotContain(行.引数一覧,引数 => string.Equals(引数.名前,"Index",StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(行.引数一覧,引数 => string.Equals(引数.名前,"Page",StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(行.引数一覧,引数 => string.Equals(引数.名前,"Segment",StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(行.引数一覧,引数 => string.Equals(引数.名前,"Memory",StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(行.引数一覧,引数 => string.Equals(引数.名前,"Fraction",StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void 仕様確認05() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.スケーラブル永続化セット);
        var 引数一覧 = 引数集合一覧を取得(メソッドを読み込む("Benchmark.Scalable.Persisted.Collections","Shared.Scalable.Persisted.Collections.Set.SetInt32","Add")).First();

        Assert.Equal("65536",global::UI.スケーラブルベンチマーク名フォーマッター.表示引数値を取得(ソースタブ,引数一覧,global::UI.スケーラブルベンチマーク引数列.Count));
        Assert.Equal(string.Empty,global::UI.スケーラブルベンチマーク名フォーマッター.表示引数値を取得(ソースタブ,引数一覧,global::UI.スケーラブルベンチマーク引数列.Index));
        Assert.Equal(string.Empty,global::UI.スケーラブルベンチマーク名フォーマッター.表示引数値を取得(ソースタブ,引数一覧,global::UI.スケーラブルベンチマーク引数列.Page));
        Assert.Equal(string.Empty,global::UI.スケーラブルベンチマーク名フォーマッター.表示引数値を取得(ソースタブ,引数一覧,global::UI.スケーラブルベンチマーク引数列.Segment));
        Assert.Equal(string.Empty,global::UI.スケーラブルベンチマーク名フォーマッター.表示引数値を取得(ソースタブ,引数一覧,global::UI.スケーラブルベンチマーク引数列.Memory));
        Assert.Equal(string.Empty,global::UI.スケーラブルベンチマーク名フォーマッター.表示引数値を取得(ソースタブ,引数一覧,global::UI.スケーラブルベンチマーク引数列.Fraction));
        Assert.Equal("1",global::UI.スケーラブルベンチマーク名フォーマッター.表示引数値を取得(ソースタブ,引数一覧,global::UI.スケーラブルベンチマーク引数列.Initial));
        Assert.Equal("4096",global::UI.スケーラブルベンチマーク名フォーマッター.表示引数値を取得(ソースタブ,引数一覧,global::UI.スケーラブルベンチマーク引数列.Data));
    }

    private static MethodInfo メソッドを読み込む(string アセンブリ名,string 型完全名,string メソッド名) =>
        Assembly.Load(new AssemblyName(アセンブリ名))
            .GetType(型完全名,throwOnError: true)!
            .GetMethod(メソッド名,BindingFlags.Public|BindingFlags.Instance)!
        ??throw new MissingMethodException(型完全名,メソッド名);

    private static IReadOnlyList<IReadOnlyList<global::UI.ベンチマーク引数>> 引数集合一覧を取得(MethodInfo メソッド) {
        var ベンチマーク引数取得メソッド = typeof(global::UI.ベンチマークカタログ).GetMethod("ベンチマーク引数を取得",BindingFlags.Static|BindingFlags.NonPublic)
            ??throw new MissingMethodException(nameof(global::UI.ベンチマークカタログ),"ベンチマーク引数を取得");
        return ((IEnumerable<IReadOnlyList<global::UI.ベンチマーク引数>>)ベンチマーク引数取得メソッド.Invoke(null,[メソッド])!).ToArray();
    }

    private static global::UI.報告行 報告行を作成(string プロジェクト,string メソッド,string 型,IReadOnlyList<global::UI.ベンチマーク引数> 引数一覧) =>
        new(
            プロジェクト,
            メソッド,
            型,
            メソッド,
            "Completed",
            "1",
            string.Empty,
            string.Empty,
            "0",
            string.Empty,
            string.Empty,
            1,
            0,
            0,
            0,
            0,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            global::UI.ベンチマーク引数フォーマッター.キーを作成(引数一覧),
            引数一覧);

}
