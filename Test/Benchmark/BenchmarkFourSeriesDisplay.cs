#if NET10_0_OR_GREATER && WINDOWS
namespace Test.Benchmark;

public sealed class ベンチマーク4系列表示 {
    [Theory, Trait("Category","BenchmarkUI")]
    [InlineData(global::UI.ベンチマークソースタブ.Seamless永続化,"FLinq(Persisted)")]
    [InlineData(global::UI.ベンチマークソースタブ.SeamlessTsavorite,"FLinq(Tsavorite)")]
    public void 四列は順序と比較基準を固定する(global::UI.ベンチマークソースタブ タブ,string ネイティブ見出し) {
        var 定義=global::UI.ベンチマークソースタブ一覧.取得(タブ);

        Assert.Equal(
            [ネイティブ見出し,"ValueEnumerable","FLinq(IEnumerable)","System.Linq"],
            定義.列一覧.Select(static 列 => 列.見出し).ToArray());
        Assert.Equal(4,定義.プロジェクト順.Count);
        Assert.Equal(定義.左プロジェクト,定義.プロジェクト順[0]);
        Assert.Equal(定義.中央プロジェクト,定義.プロジェクト順[1]);
        Assert.Equal(定義.追加プロジェクト,定義.プロジェクト順[2]);
        Assert.Equal(定義.右プロジェクト,定義.プロジェクト順[3]);
        Assert.Equal(定義.左アセンブリ名,定義.アセンブリ名を取得(定義.左プロジェクト));
        Assert.Equal(定義.右アセンブリ名,定義.アセンブリ名を取得(定義.右プロジェクト));
        Assert.Collection(
            定義.比較ペア一覧,
            ペア => {
                Assert.Equal(定義.左プロジェクト,ペア.左プロジェクト);
                Assert.Equal(定義.中央プロジェクト,ペア.右プロジェクト);
                Assert.Equal("左",ペア.最適化列キー);
                Assert.Equal("中央",ペア.基準列キー);
            },
            ペア => {
                Assert.Equal(定義.追加プロジェクト,ペア.左プロジェクト);
                Assert.Equal(定義.右プロジェクト,ペア.右プロジェクト);
                Assert.Equal("追加",ペア.最適化列キー);
                Assert.Equal("右",ペア.基準列キー);
            });
    }

    [Theory, Trait("Category","BenchmarkUI")]
    [InlineData(global::UI.ベンチマークソースタブ.Seamless永続化)]
    [InlineData(global::UI.ベンチマークソースタブ.SeamlessTsavorite)]
    public void 四列の追加列は独立測定列としてケース欠測を表現できる(global::UI.ベンチマークソースタブ タブ) {
        var 定義=global::UI.ベンチマークソースタブ一覧.取得(タブ);
        var ケース一覧=new Dictionary<string,global::UI.ベンチマークケース>(StringComparer.OrdinalIgnoreCase) {
            [定義.左プロジェクト]=ケースを作成(定義,定義.左プロジェクト),
            [定義.中央プロジェクト]=ケースを作成(定義,定義.中央プロジェクト),
            [定義.右プロジェクト]=ケースを作成(定義,定義.右プロジェクト),
        };
        var 行=new global::UI.ベンチマーク表行("int|Where_predicate_Count","Where_predicate_Count","Where_predicate_Count",定義,ケース一覧,"int");

        Assert.Equal(4,行.プロジェクトセル一覧.Count());
        Assert.False(行.追加セル.ケースありか);
        Assert.True(行.左セル.ケースありか);
        Assert.True(行.右セル.ケースありか);
    }

    [Theory, Trait("Category","BenchmarkUI")]
    [InlineData(global::UI.ベンチマークソースタブ.Seamless永続化)]
    [InlineData(global::UI.ベンチマークソースタブ.SeamlessTsavorite)]
    public void 四列のFLinqIEnumerable失敗は日時と理由を保持する(global::UI.ベンチマークソースタブ タブ) {
        var 定義=global::UI.ベンチマークソースタブ一覧.取得(タブ);
        var ケース一覧=定義.プロジェクト順.ToDictionary(
            static プロジェクト => プロジェクト,
            プロジェクト => ケースを作成(定義,プロジェクト),
            StringComparer.OrdinalIgnoreCase);
        var 行=new global::UI.ベンチマーク表行("int|Where_predicate_Count","Where_predicate_Count","Where_predicate_Count",定義,ケース一覧,"int");
        var 日時=new DateTimeOffset(2026,7,22,15,0,0,TimeSpan.Zero);
        const string 失敗理由="Worker assembly missing: Benchmark IEnumerable FLinq";
        var 結果=new global::UI.ベンチマーク結果項目(new global::UI.ベンチマークジョブ {
            プロジェクト=定義.追加プロジェクト,
            アセンブリ名=定義.アセンブリ名を取得(定義.追加プロジェクト),
            型完全名="Sample.Int32",
            メソッド名="Where_predicate_Count",
            ソースタブ=定義.タブ,
            要素数=4096,
            ループ回数=1,
            状態=global::UI.ジョブ状態.失敗,
            割り当て論理CPU=0,
            開始日時=日時,
            終了日時=日時,
            終了コード=1,
            失敗理由=失敗理由,
        });

        行.スナップショット結果を適用(定義.追加プロジェクト,結果,0,日時);

        Assert.Equal(global::UI.ベンチマーク状態画像名.失敗,行.追加セル.状態画像ソース);
        Assert.Equal("4096",行.追加セル.要素数文字列);
        Assert.NotEmpty(行.追加セル.失敗実行文字列);
        Assert.Contains(失敗理由,行.追加セル.実行ツールチップ文字列,StringComparison.Ordinal);
    }
    [Theory, Trait("Category","BenchmarkUI")]
    [InlineData(global::UI.ベンチマークソースタブ.Seamless永続化)]
    [InlineData(global::UI.ベンチマークソースタブ.SeamlessTsavorite)]
    public void 四列は二つのFLinq列だけに独立したシェアを表示する(global::UI.ベンチマークソースタブ タブ) {
        var 定義=global::UI.ベンチマークソースタブ一覧.取得(タブ);
        var ケース一覧=定義.プロジェクト順.ToDictionary(
            static プロジェクト=>プロジェクト,
            プロジェクト=>ケースを作成(定義,プロジェクト),
            StringComparer.OrdinalIgnoreCase);
        var 行=new global::UI.ベンチマーク表行("int|Count","Count","Count",定義,ケース一覧,"int");
        var 日時=new DateTimeOffset(2026,7,22,16,0,0,TimeSpan.Zero);

        行.スナップショット結果を適用(定義.左プロジェクト,結果を作成(定義,定義.左プロジェクト,100,100),0,日時);
        行.スナップショット結果を適用(定義.中央プロジェクト,結果を作成(定義,定義.中央プロジェクト,50,50),1,日時);
        行.スナップショット結果を適用(定義.追加プロジェクト,結果を作成(定義,定義.追加プロジェクト,30,30),2,日時);
        行.スナップショット結果を適用(定義.右プロジェクト,結果を作成(定義,定義.右プロジェクト,60,60),3,日時);

        Assert.Equal("0.67:0.33",行.左時間シェア表示);
        Assert.Equal("0.67:0.33",行.左メモリシェア表示);
        Assert.Equal("-",行.中央時間シェア表示);
        Assert.Equal("-",行.中央メモリシェア表示);
        Assert.Equal("0.33:0.67",行.追加時間シェア表示);
        Assert.Equal("0.33:0.67",行.追加メモリシェア表示);
        Assert.Equal(Color.FromArgb("#DC2626"),行.左時間文字色);
        Assert.Equal(Color.FromArgb("#2563EB"),行.中央時間文字色);
        Assert.Equal(Color.FromArgb("#2563EB"),行.追加時間文字色);
        Assert.Equal(Color.FromArgb("#DC2626"),行.右時間文字色);
        Assert.False(行.ソート値を取得(global::UI.ベンチマークグリッドソート列.左時間シェア).欠測か);
        Assert.False(行.ソート値を取得(global::UI.ベンチマークグリッドソート列.追加時間シェア).欠測か);
    }

    [Theory, Trait("Category","BenchmarkUI")]
    [InlineData(global::UI.ベンチマークソースタブ.Seamless永続化)]
    [InlineData(global::UI.ベンチマークソースタブ.SeamlessTsavorite)]
    public void 四列の改善候補は比較ペアごとに分離される(global::UI.ベンチマークソースタブ タブ) {
        var 定義=global::UI.ベンチマークソースタブ一覧.取得(タブ);
        var 左=new global::UI.プロジェクト測定値(100,100,true);
        var 中央=new global::UI.プロジェクト測定値(50,50,true);
        var 追加=new global::UI.プロジェクト測定値(80,80,true);
        var 右=new global::UI.プロジェクト測定値(40,40,true);
        var 比較一覧=定義.比較ペア一覧.Select(ペア=>{
            var 比較左=ペア.最適化列キー=="左" ? 左 : 追加;
            var 比較右=ペア.基準列キー=="中央" ? 中央 : 右;
            return new global::UI.シェア比較(
                ペア,
                比較左,
                比較右,
                global::UI.シェア値.時間から作成(比較左.時間ナノ秒!.Value,比較右.時間ナノ秒!.Value),
                global::UI.シェア値.メモリから作成(比較左.割り当てバイト数!.Value,比較右.割り当てバイト数!.Value));
        }).ToArray();
        var 行=new global::UI.ベンチマーク比較行("Count",定義,左,右,中央,追加) {
            メソッド名="Count",
            型完全名="int",
            左右シェア=比較一覧[0],
            比較一覧=比較一覧,
        };

        var 候補一覧=global::UI.ベンチマーク改善候補ビルダー.比較行から候補を構築([行],4096);

        Assert.Equal(2,候補一覧.Count);
        Assert.Equal(2,候補一覧.Select(static 候補=>候補.キー).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(定義.比較ペア一覧.Select(static ペア=>ペア.キー).Order(),候補一覧.Select(候補=>候補.比較ペア.キー).Order());
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
        new(プロジェクト,定義.アセンブリ名を取得(プロジェクト),"Sample.Int32","Where_predicate_Count");
}
#endif
