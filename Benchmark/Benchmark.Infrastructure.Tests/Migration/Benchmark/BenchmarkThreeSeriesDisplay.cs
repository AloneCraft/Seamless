#if NET10_0_OR_GREATER && WINDOWS
namespace Test.Benchmark;

public sealed class ベンチマーク2系列表示 {
    [Fact, Trait("Category","BenchmarkUI")]
    public void 右列を基準にシェアと比較文字色を表示する() {
        var ソースタブ=global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var ケース一覧=new Dictionary<string,global::UI.ベンチマークケース>(StringComparer.OrdinalIgnoreCase) {
            [ソースタブ.左プロジェクト]=ケースを作成(ソースタブ.左プロジェクト,ソースタブ.左アセンブリ名),
            [ソースタブ.右プロジェクト]=ケースを作成(ソースタブ.右プロジェクト,ソースタブ.右アセンブリ名),
        };
        var 行=new global::UI.ベンチマーク表行(
            "int|Where_predicate_Count",
            "Where_predicate_Count",
            "Where_predicate_Count",
            ソースタブ,
            ケース一覧,
            "int");
        var 計測日時=new DateTimeOffset(2026,7,16,0,0,0,TimeSpan.Zero);

        行.スナップショット結果を適用(ソースタブ.左プロジェクト,結果を作成(ソースタブ,ソースタブ.左プロジェクト,50,1),0,計測日時);
        Assert.Equal("-",行.時間シェア表示);
        Assert.Equal("-",行.メモリシェア表示);
        Assert.Equal(Color.FromArgb("#1E293B"),行.左時間文字色);
        Assert.Equal(Color.FromArgb("#1E293B"),行.右時間文字色);

        行.スナップショット結果を適用(ソースタブ.右プロジェクト,結果を作成(ソースタブ,ソースタブ.右プロジェクト,100,0),1,計測日時);

        Assert.Equal("0.33:0.67",行.時間シェア表示);
        Assert.Equal("1.00:0.00",行.メモリシェア表示);
        Assert.True(行.時間シェアゲージ.色付きバーがあるか);
        Assert.Equal(50.0/150,行.時間シェアゲージ.左割合,12);
        Assert.Equal(100.0/150,行.時間シェアゲージ.右割合,12);
        Assert.Equal(Color.FromArgb("#2563EB"),行.左時間文字色);
        Assert.Equal(Color.FromArgb("#DC2626"),行.右時間文字色);
        Assert.Equal(Color.FromArgb("#DC2626"),行.左メモリ文字色);
        Assert.Equal(Color.FromArgb("#2563EB"),行.右メモリ文字色);
    }

    [Fact, Trait("Category","BenchmarkUI")]
    public void 二列の比較文字色は小さい値を青同値を黒で表示する() {
        var ソースタブ=global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var ケース一覧=new Dictionary<string,global::UI.ベンチマークケース>(StringComparer.OrdinalIgnoreCase) {
            [ソースタブ.左プロジェクト]=ケースを作成(ソースタブ.左プロジェクト,ソースタブ.左アセンブリ名),
            [ソースタブ.右プロジェクト]=ケースを作成(ソースタブ.右プロジェクト,ソースタブ.右アセンブリ名),
        };
        var 行=new global::UI.ベンチマーク表行("int|Where_predicate_Count","Where_predicate_Count","Where_predicate_Count",ソースタブ,ケース一覧,"int");
        var 計測日時=new DateTimeOffset(2026,7,16,0,0,0,TimeSpan.Zero);

        行.スナップショット結果を適用(ソースタブ.左プロジェクト,結果を作成(ソースタブ,ソースタブ.左プロジェクト,100,200),0,計測日時);
        行.スナップショット結果を適用(ソースタブ.右プロジェクト,結果を作成(ソースタブ,ソースタブ.右プロジェクト,100,200),1,計測日時);
        Assert.Equal(Color.FromArgb("#1E293B"),行.左時間文字色);
        Assert.Equal(Color.FromArgb("#1E293B"),行.右時間文字色);
        Assert.Equal(Color.FromArgb("#1E293B"),行.左メモリ文字色);
        Assert.Equal(Color.FromArgb("#1E293B"),行.右メモリ文字色);

        行.スナップショット結果を適用(ソースタブ.左プロジェクト,結果を作成(ソースタブ,ソースタブ.左プロジェクト,50,100),0,計測日時);
        行.スナップショット結果を適用(ソースタブ.右プロジェクト,結果を作成(ソースタブ,ソースタブ.右プロジェクト,100,200),1,計測日時);
        Assert.Equal(Color.FromArgb("#2563EB"),行.左時間文字色);
        Assert.Equal(Color.FromArgb("#DC2626"),行.右時間文字色);
        Assert.Equal(Color.FromArgb("#2563EB"),行.左メモリ文字色);
        Assert.Equal(Color.FromArgb("#DC2626"),行.右メモリ文字色);
    }

    private static global::UI.ベンチマークケース ケースを作成(string プロジェクト,string アセンブリ名) =>
        new(プロジェクト,アセンブリ名,"Sample.Int32","Where_predicate_Count");

    private static global::UI.ベンチマーク結果項目 結果を作成(
        global::UI.ベンチマークソースタブ定義 ソースタブ,
        string プロジェクト,
        double 平均ナノ秒,
        long 割り当てバイト数) =>
        new(new global::UI.ベンチマークジョブ {
            プロジェクト=プロジェクト,
            アセンブリ名=プロジェクト==ソースタブ.左プロジェクト
                ? ソースタブ.左アセンブリ名
                : ソースタブ.右アセンブリ名,
            型完全名="Sample.Int32",
            メソッド名="Where_predicate_Count",
            ソースタブ=ソースタブ.タブ,
            要素数=4096,
            ループ回数=1,
            状態=global::UI.ジョブ状態.完了,
            平均ナノ秒=平均ナノ秒,
            割り当てバイト数=割り当てバイト数,
            終了コード=0,
        });
}
#endif