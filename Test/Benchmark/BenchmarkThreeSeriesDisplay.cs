#if NET10_0_OR_GREATER && WINDOWS
namespace Test.Benchmark;

public sealed class ベンチマーク3系列表示 {
    [Fact, Trait("Category","BenchmarkUI")]
    public void 右列を基準にシェアと比較文字色を表示する() {
        var ソースタブ=global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var ケース一覧=new Dictionary<string,global::UI.ベンチマークケース>(StringComparer.OrdinalIgnoreCase) {
            [ソースタブ.左プロジェクト]=ケースを作成(ソースタブ.左プロジェクト,ソースタブ.左アセンブリ名),
            [ソースタブ.中央プロジェクト]=ケースを作成(ソースタブ.中央プロジェクト,ソースタブ.中央アセンブリ名),
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

        Assert.Equal("-",行.左時間シェア表示);
        Assert.Equal("-",行.中央時間シェア表示);

        行.スナップショット結果を適用(ソースタブ.中央プロジェクト,結果を作成(ソースタブ,ソースタブ.中央プロジェクト,104,0),1,計測日時);

        // 右列が未確定の間は、左・中央とも右列基準のシェアを表示しない。
        Assert.Equal("-",行.左時間シェア表示);
        Assert.Equal("-",行.左メモリシェア表示);
        Assert.Equal("-",行.中央時間シェア表示);
        Assert.False(行.左時間シェアゲージ.色付きバーがあるか);
        Assert.False(行.中央時間シェアゲージ.色付きバーがあるか);

        行.スナップショット結果を適用(ソースタブ.右プロジェクト,結果を作成(ソースタブ,ソースタブ.右プロジェクト,100,0),2,計測日時);

        // 左:右と中央:右が確定する。中央は右より3%超遅いため警告が付く。
        Assert.Equal("0.33:0.67",行.左時間シェア表示);
        Assert.Equal("1.00:0.00",行.左メモリシェア表示);
        Assert.True(行.左時間シェアゲージ.色付きバーがあるか);
        Assert.Equal(50.0/150,行.左時間シェアゲージ.左割合,12);
        Assert.Equal(100.0/150,行.左時間シェアゲージ.右割合,12);
        Assert.Equal("0.51:0.49",行.中央時間シェア表示);
        Assert.Equal("0.00:0.00",行.中央メモリシェア表示);
        Assert.True(行.中央時間シェアゲージ.色付きバーがあるか);
        Assert.Equal(104.0/204,行.中央時間シェアゲージ.左割合,12);
        Assert.Equal(100.0/204,行.中央時間シェアゲージ.右割合,12);
        Assert.Equal(Color.FromArgb("#2563EB"),行.左時間文字色);
        Assert.Equal(Color.FromArgb("#1E293B"),行.右時間文字色);
        Assert.Equal(Color.FromArgb("#DC2626"),行.左メモリ文字色);
        Assert.Equal(Color.FromArgb("#1E293B"),行.右メモリ文字色);
        Assert.Equal(Color.FromArgb("#DC2626"),行.中央時間文字色);
        Assert.Equal(Color.FromArgb("#1E293B"),行.中央メモリ文字色);
    }

    [Fact, Trait("Category","BenchmarkUI")]
    public void 三列の比較文字色は全列順位と同率と欠測を反映する() {
        var 行=三列の行を作成();
        var ソースタブ=行.ソースタブ;
        var 計測日時=new DateTimeOffset(2026,7,16,0,0,0,TimeSpan.Zero);

        void 三列結果を適用(double 左時間,long 左メモリ,double 中央時間,long 中央メモリ,double 右時間,long 右メモリ) {
            行.スナップショット結果を適用(ソースタブ.左プロジェクト,結果を作成(ソースタブ,ソースタブ.左プロジェクト,左時間,左メモリ),0,計測日時);
            行.スナップショット結果を適用(ソースタブ.中央プロジェクト,結果を作成(ソースタブ,ソースタブ.中央プロジェクト,中央時間,中央メモリ),1,計測日時);
            行.スナップショット結果を適用(ソースタブ.右プロジェクト,結果を作成(ソースタブ,ソースタブ.右プロジェクト,右時間,右メモリ),2,計測日時);
        }

        void 全列が黒か() {
            Assert.Equal(Color.FromArgb("#1E293B"),行.左時間文字色);
            Assert.Equal(Color.FromArgb("#1E293B"),行.中央時間文字色);
            Assert.Equal(Color.FromArgb("#1E293B"),行.右時間文字色);
            Assert.Equal(Color.FromArgb("#1E293B"),行.左メモリ文字色);
            Assert.Equal(Color.FromArgb("#1E293B"),行.中央メモリ文字色);
            Assert.Equal(Color.FromArgb("#1E293B"),行.右メモリ文字色);
        }

        行.スナップショット結果を適用(ソースタブ.左プロジェクト,結果を作成(ソースタブ,ソースタブ.左プロジェクト,30,300),0,計測日時);
        行.スナップショット結果を適用(ソースタブ.右プロジェクト,結果を作成(ソースタブ,ソースタブ.右プロジェクト,20,200),2,計測日時);
        全列が黒か();

        // 中央が唯一の最小、左が唯一の最大、右が中間。
        三列結果を適用(30,300,10,100,20,200);
        Assert.Equal(Color.FromArgb("#DC2626"),行.左時間文字色);
        Assert.Equal(Color.FromArgb("#2563EB"),行.中央時間文字色);
        Assert.Equal(Color.FromArgb("#1E293B"),行.右時間文字色);
        Assert.Equal(Color.FromArgb("#DC2626"),行.左メモリ文字色);
        Assert.Equal(Color.FromArgb("#2563EB"),行.中央メモリ文字色);
        Assert.Equal(Color.FromArgb("#1E293B"),行.右メモリ文字色);

        // 左、中央、右の各列が唯一の最小・最大になる配置。
        三列結果を適用(10,100,20,200,30,300);
        Assert.Equal(Color.FromArgb("#2563EB"),行.左時間文字色);
        Assert.Equal(Color.FromArgb("#1E293B"),行.中央時間文字色);
        Assert.Equal(Color.FromArgb("#DC2626"),行.右時間文字色);
        Assert.Equal(Color.FromArgb("#2563EB"),行.左メモリ文字色);
        Assert.Equal(Color.FromArgb("#1E293B"),行.中央メモリ文字色);
        Assert.Equal(Color.FromArgb("#DC2626"),行.右メモリ文字色);

        三列結果を適用(20,200,30,300,10,100);
        Assert.Equal(Color.FromArgb("#1E293B"),行.左時間文字色);
        Assert.Equal(Color.FromArgb("#DC2626"),行.中央時間文字色);
        Assert.Equal(Color.FromArgb("#2563EB"),行.右時間文字色);
        Assert.Equal(Color.FromArgb("#1E293B"),行.左メモリ文字色);
        Assert.Equal(Color.FromArgb("#DC2626"),行.中央メモリ文字色);
        Assert.Equal(Color.FromArgb("#2563EB"),行.右メモリ文字色);

        // 最小同率、最大同率、全同値はいずれも同率列を黒にする。
        三列結果を適用(10,100,10,100,20,200);
        Assert.Equal(Color.FromArgb("#1E293B"),行.左時間文字色);
        Assert.Equal(Color.FromArgb("#1E293B"),行.中央時間文字色);
        Assert.Equal(Color.FromArgb("#DC2626"),行.右時間文字色);
        Assert.Equal(Color.FromArgb("#1E293B"),行.左メモリ文字色);
        Assert.Equal(Color.FromArgb("#1E293B"),行.中央メモリ文字色);
        Assert.Equal(Color.FromArgb("#DC2626"),行.右メモリ文字色);

        三列結果を適用(10,100,20,200,20,200);
        Assert.Equal(Color.FromArgb("#2563EB"),行.左時間文字色);
        Assert.Equal(Color.FromArgb("#1E293B"),行.中央時間文字色);
        Assert.Equal(Color.FromArgb("#1E293B"),行.右時間文字色);
        Assert.Equal(Color.FromArgb("#2563EB"),行.左メモリ文字色);
        Assert.Equal(Color.FromArgb("#1E293B"),行.中央メモリ文字色);
        Assert.Equal(Color.FromArgb("#1E293B"),行.右メモリ文字色);

        三列結果を適用(10,100,10,100,10,100);
        全列が黒か();
    }

    [Fact, Trait("Category","BenchmarkUI")]
    public void 二列の比較文字色は小さい値を青同値と欠測を黒で表示する() {
        var ソースタブ=global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.最適化書き換え);
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

        行.スナップショット結果を適用(ソースタブ.左プロジェクト,結果を作成(ソースタブ,ソースタブ.左プロジェクト,100,200),0,計測日時);
        Assert.Equal(Color.FromArgb("#1E293B"),行.左時間文字色);
        Assert.Equal(Color.FromArgb("#1E293B"),行.右時間文字色);
        Assert.Equal(Color.FromArgb("#1E293B"),行.左メモリ文字色);
        Assert.Equal(Color.FromArgb("#1E293B"),行.右メモリ文字色);

        行.スナップショット結果を適用(ソースタブ.右プロジェクト,結果を作成(ソースタブ,ソースタブ.右プロジェクト,100,200),1,計測日時);
        Assert.Equal(Color.FromArgb("#1E293B"),行.左時間文字色);
        Assert.Equal(Color.FromArgb("#1E293B"),行.右時間文字色);
        Assert.Equal(Color.FromArgb("#1E293B"),行.左メモリ文字色);
        Assert.Equal(Color.FromArgb("#1E293B"),行.右メモリ文字色);

        行.スナップショット結果を適用(ソースタブ.右プロジェクト,結果を作成(ソースタブ,ソースタブ.右プロジェクト,200,100),1,計測日時);
        Assert.Equal(Color.FromArgb("#2563EB"),行.左時間文字色);
        Assert.Equal(Color.FromArgb("#DC2626"),行.右時間文字色);
        Assert.Equal(Color.FromArgb("#DC2626"),行.左メモリ文字色);
        Assert.Equal(Color.FromArgb("#2563EB"),行.右メモリ文字色);
    }

    private static global::UI.ベンチマーク表行 三列の行を作成() {
        var ソースタブ=global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var ケース一覧=new Dictionary<string,global::UI.ベンチマークケース>(StringComparer.OrdinalIgnoreCase) {
            [ソースタブ.左プロジェクト]=ケースを作成(ソースタブ.左プロジェクト,ソースタブ.左アセンブリ名),
            [ソースタブ.中央プロジェクト]=ケースを作成(ソースタブ.中央プロジェクト,ソースタブ.中央アセンブリ名),
            [ソースタブ.右プロジェクト]=ケースを作成(ソースタブ.右プロジェクト,ソースタブ.右アセンブリ名),
        };
        return new global::UI.ベンチマーク表行(
            "int|Where_predicate_Count",
            "Where_predicate_Count",
            "Where_predicate_Count",
            ソースタブ,
            ケース一覧,
            "int");
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
                : プロジェクト==ソースタブ.中央プロジェクト
                    ? ソースタブ.中央アセンブリ名
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
