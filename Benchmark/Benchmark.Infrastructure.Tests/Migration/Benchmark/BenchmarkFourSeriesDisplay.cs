#if NET10_0_OR_GREATER && WINDOWS
namespace Test.Benchmark;

public sealed class ベンチマークプロバイダー2系列表示 {
    [Theory,Trait("Category","BenchmarkUI")]
    [InlineData(global::UI.ベンチマークソースタブ.Seamless永続化,"Persisted(Seamless)","Seamless.Persisted.FLinq","Seamless.Persisted.ValueLinq","Persisted(Seamless)")]
    [InlineData(global::UI.ベンチマークソースタブ.System永続化,"Persisted(System)","Seamless.Persisted.IEnumerable.FLinq","Seamless.Persisted.Linq","Persisted(System)")]
    [InlineData(global::UI.ベンチマークソースタブ.SeamlessTsavorite,"Tsavorite(Seamless)","Seamless.Tsavorite.FLinq","Seamless.Tsavorite.ValueLinq","Tsavorite(Seamless)")]
    [InlineData(global::UI.ベンチマークソースタブ.SystemTsavorite,"Tsavorite(System)","Seamless.Tsavorite.IEnumerable.FLinq","Seamless.Tsavorite.Linq","Tsavorite(System)")]
    public void プロバイダータブはFLinqと対象Enumerableの二列比較に分割される(global::UI.ベンチマークソースタブ タブ,string タブ名,string 左プロジェクト,string 右プロジェクト,string 右見出し) {
        var 定義=global::UI.ベンチマークソースタブ一覧.取得(タブ);

        Assert.Equal(タブ名,定義.表示名);
        Assert.Equal(["FLinq",右見出し],定義.列一覧.Select(static 列=>列.見出し).ToArray());
        Assert.Equal([左プロジェクト,右プロジェクト],定義.プロジェクト順);
        var 比較=Assert.Single(定義.比較ペア一覧);
        Assert.Equal(左プロジェクト,比較.左プロジェクト);
        Assert.Equal(右プロジェクト,比較.右プロジェクト);
        Assert.Equal("左",比較.最適化列キー);
        Assert.Equal("右",比較.基準列キー);
    }

    [Theory,Trait("Category","BenchmarkUI")]
    [InlineData(global::UI.ベンチマークソースタブ.Seamless永続化)]
    [InlineData(global::UI.ベンチマークソースタブ.System永続化)]
    [InlineData(global::UI.ベンチマークソースタブ.SeamlessTsavorite)]
    [InlineData(global::UI.ベンチマークソースタブ.SystemTsavorite)]
    public void プロバイダータブはFLinq列にだけシェアを表示する(global::UI.ベンチマークソースタブ タブ) {
        var 定義=global::UI.ベンチマークソースタブ一覧.取得(タブ);
        var ケース一覧=定義.プロジェクト順.ToDictionary(
            static プロジェクト=>プロジェクト,
            プロジェクト=>ケースを作成(定義,プロジェクト),
            StringComparer.OrdinalIgnoreCase);
        var 行=new global::UI.ベンチマーク表行("int|Count","Count","Count",定義,ケース一覧,"int");
        var 日時=new DateTimeOffset(2026,7,22,16,0,0,TimeSpan.Zero);

        行.スナップショット結果を適用(定義.左プロジェクト,結果を作成(定義,定義.左プロジェクト,100,100),0,日時);
        行.スナップショット結果を適用(定義.右プロジェクト,結果を作成(定義,定義.右プロジェクト,50,50),1,日時);

        Assert.Equal("0.67:0.33",行.時間シェア表示);
        Assert.Equal("0.67:0.33",行.メモリシェア表示);
        Assert.Equal(Color.FromArgb("#DC2626"),行.左時間文字色);
        Assert.Equal(Color.FromArgb("#2563EB"),行.右時間文字色);
        Assert.False(行.ソート値を取得(global::UI.ベンチマークグリッドソート列.時間シェア).欠測か);
    }

    private static global::UI.ベンチマーク結果項目 結果を作成(global::UI.ベンチマークソースタブ定義 定義,string プロジェクト,double 時間,long メモリ) =>
        new(new global::UI.ベンチマークジョブ {
            プロジェクト=プロジェクト,
            アセンブリ名=定義.アセンブリ名を取得(プロジェクト),
            型完全名="Sample.Int32",
            メソッド名="Count",
            ソースタブ=定義.タブ,
            要素数=4096,
            ループ回数=1,
            状態=global::UI.ジョブ状態.完了,
            平均ナノ秒=時間,
            割り当てバイト数=メモリ,
            開始日時=DateTimeOffset.UtcNow,
            終了日時=DateTimeOffset.UtcNow,
        });

    private static global::UI.ベンチマークケース ケースを作成(global::UI.ベンチマークソースタブ定義 定義,string プロジェクト) =>
        new(プロジェクト,定義.アセンブリ名を取得(プロジェクト),"Sample.Int32","Count");
}
#endif