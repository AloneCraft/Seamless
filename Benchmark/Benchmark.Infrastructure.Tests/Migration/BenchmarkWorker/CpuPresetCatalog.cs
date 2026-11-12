using UI;

namespace Test.BenchmarkWorker;

[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class CPUプリセットカタログ {
    [Fact]
    public void 仕様確認01() {
        var 高性能主スレッド = new CPUコア情報(0,CPUコア種別.高性能,0,0,1,true);
        var 高性能兄弟スレッド = new CPUコア情報(1,CPUコア種別.高性能,0,0,2,true);
        var 高効率 = new CPUコア情報(16,CPUコア種別.高効率,8,0,0,true);
        var 不明 = new CPUコア情報(30,CPUコア種別.不明,15,15,0,false);

        Assert.Equal("0(Pコア0)",高性能主スレッド.表示文字列);
        Assert.Equal("1(Pコア0)",高性能兄弟スレッド.表示文字列);
        Assert.Equal("16(Eコア0)",高効率.表示文字列);
        Assert.Equal("30(不明なコア15)",不明.表示文字列);
    }

    [Fact]
    public void 仕様確認02() {
        var CPUコア一覧 = ハイブリッドCPU集合を作成();

        var プリセット一覧 = global::UI.CPUプリセットカタログ.構築(CPUコア一覧)
            .ToDictionary(static プリセット => プリセット.キー,static プリセット => プリセット.論理プロセッサインデックス一覧.ToArray(),StringComparer.Ordinal);

        Assert.Equal(Enumerable.Range(0,28).ToArray(),プリセット一覧["ALL"]);
        Assert.Empty(プリセット一覧["CLEAR"]);
        Assert.Equal(["すべて","クリア"],global::UI.CPUプリセットカタログ.構築(CPUコア一覧).Take(2).Select(static プリセット=>プリセット.ラベル).ToArray());
        Assert.Equal([0,2,4,6,8,10,12,14],プリセット一覧["P1T"]);
        Assert.Equal(Enumerable.Range(0,16).ToArray(),プリセット一覧["P2T"]);
        Assert.Equal([16,20,24],プリセット一覧["E1T"]);
        Assert.Equal([16,17,20,21,24,25],プリセット一覧["E2T"]);
        Assert.Equal([16,17,18,20,21,22,24,25,26],プリセット一覧["E3T"]);
        Assert.Equal(Enumerable.Range(16,12).ToArray(),プリセット一覧["E4T"]);
    }

    [Fact]
    public void 仕様確認03() {
        CPUコア情報[] CPUコア一覧 = [
            new(0,CPUコア種別.高効率,0,0,0,true),
            new(1,CPUコア種別.高効率,0,0,0,true),
            new(2,CPUコア種別.高効率,1,1,0,true),
            new(3,CPUコア種別.高効率,1,1,0,true),
        ];

        var プリセットキー一覧 = global::UI.CPUプリセットカタログ.構築(CPUコア一覧)
            .Select(static プリセット => プリセット.キー)
            .ToArray();

        Assert.Equal(["ALL","CLEAR","E1T","E2T"],プリセットキー一覧);
    }

    [Fact]
    public void 仕様確認04() {
        CPUコア情報[] 高性能のみ = [
            new(0,CPUコア種別.高性能,0,0,0,true),
            new(1,CPUコア種別.高性能,0,0,0,true),
            new(2,CPUコア種別.高性能,1,1,0,true),
            new(3,CPUコア種別.高性能,1,1,0,true),
        ];
        CPUコア情報[] 不明のみ = [
            new(0,CPUコア種別.不明,0,0,0,false),
            new(1,CPUコア種別.不明,1,1,0,false),
        ];

        Assert.Equal(["ALL","CLEAR","P1T","P2T"],global::UI.CPUプリセットカタログ.構築(高性能のみ).Select(static プリセット => プリセット.キー).ToArray());
        Assert.Equal(["ALL","CLEAR"],global::UI.CPUプリセットカタログ.構築(不明のみ).Select(static プリセット => プリセット.キー).ToArray());
    }

    [Fact]
    public void 仕様確認05() {
        var CPUコア一覧 = ハイブリッドCPU集合を作成()
            .Concat([
                new CPUコア情報(28,CPUコア種別.不明,11,0,0,true),
                new CPUコア情報(29,CPUコア種別.不明,11,0,0,true),
            ])
            .ToArray();

        var 区画一覧 = CPU選択表ロジック.区画一覧を作成(CPUコア一覧);

        Assert.Equal(["Pコア","Eコア","不明なコア"],区画一覧.Select(static 区画 => 区画.タイトル).ToArray());
        Assert.Equal(8,区画一覧[0].行一覧.Count);
        Assert.Equal(2,区画一覧[0].列数);
        Assert.Equal("0-1",区画一覧[0].行一覧[0].ラベル);
        Assert.Equal([0,1],区画一覧[0].行一覧[0].論理プロセッサインデックス一覧);
        Assert.Equal(3,区画一覧[1].行一覧.Count);
        Assert.Equal(4,区画一覧[1].列数);
        Assert.Equal("16-19",区画一覧[1].行一覧[0].ラベル);
        Assert.Equal("28-29",区画一覧[2].行一覧[0].ラベル);
    }

    [Fact]
    public void 仕様確認06() {
        var CPUコア一覧 = ハイブリッドCPU集合を作成().ToArray();
        CPUコア一覧[0].選択中か=true;
        CPUコア一覧[5].選択中か=true;

        CPU選択表ロジック.一括選択を適用(CPUコア一覧,[0,5],false);

        Assert.Single(CPUコア一覧,static CPU => CPU.選択中か);
        Assert.True(CPUコア一覧[0].選択中か);

        CPU選択表ロジック.一括選択を適用(CPUコア一覧,[16,17,18,19],true);

        Assert.True(CPUコア一覧[0].選択中か);
        Assert.True(CPUコア一覧[16].選択中か);
        Assert.True(CPUコア一覧[17].選択中か);
        Assert.True(CPUコア一覧[18].選択中か);
        Assert.True(CPUコア一覧[19].選択中か);
    }

    [Fact]
    public void 仕様確認07() {
        var 値一覧 = new[]{
            ("missing",UIソート値.欠測),
            ("two",UIソート値.数値から作成(2)),
            ("one",UIソート値.数値から作成(1)),
        };

        var 昇順 = 値一覧
            .OrderBy(static 項目 => 項目.Item2,Comparer<UIソート値>.Create((左,右) => UIソート値.比較(左,右,false)))
            .Select(static 項目 => 項目.Item1)
            .ToArray();
        var 降順 = 値一覧
            .OrderBy(static 項目 => 項目.Item2,Comparer<UIソート値>.Create((左,右) => UIソート値.比較(左,右,true)))
            .Select(static 項目 => 項目.Item1)
            .ToArray();

        Assert.Equal(["one","two","missing"],昇順);
        Assert.Equal(["two","one","missing"],降順);
    }

    [Fact]
    public void 仕様確認08() {
        Assert.True(ベンチマークプロジェクト実行状態ロジック.待機中か(ベンチマークプロジェクト実行状態.待機中,false));
        Assert.False(ベンチマークプロジェクト実行状態ロジック.待機中か(ベンチマークプロジェクト実行状態.待機中,true));
        Assert.True(ベンチマークプロジェクト実行状態ロジック.実行中か(ベンチマークプロジェクト実行状態.実行中));
        Assert.False(ベンチマークプロジェクト実行状態ロジック.実行中か(ベンチマークプロジェクト実行状態.完了));
        Assert.True(ベンチマークプロジェクト実行状態ロジック.完了か(ベンチマークプロジェクト実行状態.完了));
        Assert.False(ベンチマークプロジェクト実行状態ロジック.完了か(ベンチマークプロジェクト実行状態.待機中));
    }

    [Fact]
    public void 仕様確認09() {
        var 次 = ベンチマーク測定強調ロジック.実行要素数へ進める(
            ベンチマーク測定強調状態.新規,
            4,
            4);

        Assert.Equal(ベンチマーク測定強調状態.新規,次);
    }

    [Fact]
    public void 仕様確認10() {
        var 次 = ベンチマーク測定強調ロジック.実行要素数へ進める(
            ベンチマーク測定強調状態.新規,
            4,
            8);

        Assert.Equal(ベンチマーク測定強調状態.実行中保持,次);
    }

    [Fact]
    public void 仕様確認11() {
        var 次 = ベンチマーク測定強調ロジック.実行後にクリア();

        Assert.Equal(ベンチマーク測定強調状態.なし,次);
    }

    [Fact]
    public void 仕様確認12() {
        var 行一覧 = new[]{
            報告行を作成("System.Array.FLinq","Select","Completed",5,10),
            報告行を作成("System.Array.ZLinq","Select","Completed",4,0),
        };

        var 比較 = Assert.Single(ベンチマーク比較フォーマッター.比較行を構築(行一覧));

        Assert.Equal("0.56:0.44",比較.左右シェア.時間シェア.表示文字列);
        Assert.Equal("1.00:0.00",比較.左右シェア.メモリシェア.表示文字列);
        Assert.Equal(ベンチマークソースタブ.System配列,比較.ソースタブ.タブ);
    }

    [Fact]
    public void 仕様確認13() {
        var 行一覧 = ベンチマーク比較フォーマッター.比較行を構築([
            報告行を作成("System.Array.FLinq","Select","Completed",5,10),
            報告行を作成("System.Array.ZLinq","Select","Completed",4,0),
        ]);

        var 読込項目 = Assert.Single(行一覧);

        Assert.Equal(5,読込項目.左.時間ナノ秒);
        Assert.Equal(0,読込項目.右.割り当てバイト数);
        Assert.Equal("0.56:0.44",読込項目.左右シェア.時間シェア.表示文字列);
    }

    [Fact]
    public void 仕様確認14() {
        var 行一覧 = ベンチマーク比較フォーマッター.比較行を構築([
            報告行を作成("System.Array.FLinq","Select","Failed",5,10),
            報告行を作成("System.Array.ZLinq","Select","Completed",4,0),
        ]);

        var 比較 = Assert.Single(行一覧);
        Assert.False(比較.左.測定値ありか);
        Assert.True(比較.右.測定値ありか);
        Assert.Equal("-",比較.左右シェア.時間シェア.表示文字列);
    }

    [Fact]
    public void 仕様確認15() {
        var 行一覧 = ベンチマーク比較フォーマッター.比較行を構築([
            報告行を作成("System.Array.FLinq","Select","Completed",0,10),
            報告行を作成("System.Array.ZLinq","Select","Completed",4,0),
        ]);

        var 比較 = Assert.Single(行一覧);

        Assert.False(比較.左.測定値ありか);
        Assert.True(比較.右.測定値ありか);
        Assert.Equal("-",比較.左右シェア.時間シェア.表示文字列);
    }

    [Fact]
    public void 仕様確認16() {
        var 読込項目 = Assert.Single(ベンチマーク比較フォーマッター.比較行を構築([
            報告行を作成("System.Array.FLinq","Select","Completed",0,10),
            報告行を作成("System.Array.ZLinq","Select","Completed",4,0),
        ]));

        Assert.False(読込項目.左.測定値ありか);
        Assert.True(読込項目.右.測定値ありか);
        Assert.Equal(0,読込項目.右.割り当てバイト数);
    }

    [Fact]
    public void 仕様確認17() {
        var 読込項目 = Assert.Single(ベンチマーク比較フォーマッター.比較行を構築([
            報告行を作成("System.Array.FLinq","Select","Completed",4400,0),
            報告行を作成("System.Array.ZLinq","Select","Completed",6800,0),
        ]));

        Assert.Equal(0,読込項目.左.割り当てバイト数);
        Assert.Equal(0,読込項目.右.割り当てバイト数);
    }

    [Fact]
    public void 仕様確認18() {
        var 設定 = ベンチマークワーカーランタイム.設定を作成("artifacts");

        Assert.True(ベンチマークワーカーランタイム.デバッグベンチマークを許可するか(設定));
        Assert.False(ベンチマークワーカーランタイム.使用可能な統計か(null));
    }

    [Fact]
    public void 仕様確認19() {
        Assert.Throws<InvalidDataException>(() => ベンチマーク比較フォーマッター.比較行を構築([
            報告行を作成("System LINQ","Select","Completed",1,0),
        ]));
        Assert.Throws<InvalidDataException>(() => ベンチマーク比較フォーマッター.比較行を構築([
            報告行を作成("System FLinq","Select","Completed",1,0),
        ]));
    }

    [Fact]
    public void 仕様確認20() {
        var システムタブ = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.System配列);
        var Tsavoriteタブ = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.SeamlessTsavorite);

        Assert.Throws<InvalidDataException>(() => ベンチマーク比較フォーマッター.比較行を構築([
            報告行を作成(システムタブ.左プロジェクト,"Select","Completed",1,0),
            報告行を作成(Tsavoriteタブ.左プロジェクト,"Select","Completed",1,0),
        ],システムタブ));
    }

    [Fact]
    public void 仕様確認21() {
        var 勝利 = シェア値.時間から作成(0.94,1);
        var 同等下限 = シェア値.時間から作成(0.95,1);
        var 同等上限 = シェア値.時間から作成(1.05,1);
        var 敗北 = シェア値.時間から作成(1.06,1);

        Assert.Equal(シェア判定.左優位,勝利.判定);
        Assert.Equal(シェア判定.同等,同等下限.判定);
        Assert.Equal(シェア判定.同等,同等上限.判定);
        Assert.Equal(シェア判定.右優位,敗北.判定);
    }

    private static IReadOnlyList<CPUコア情報> ハイブリッドCPU集合を作成() {
        var 項目一覧 = new List<CPUコア情報>();
        for(var コアインデックス = 0;コアインデックス<8;コアインデックス++) {
            var 論理先頭 = コアインデックス*2;
            項目一覧.Add(new CPUコア情報(論理先頭,CPUコア種別.高性能,コアインデックス,コアインデックス,0,true));
            項目一覧.Add(new CPUコア情報(論理先頭+1,CPUコア種別.高性能,コアインデックス,コアインデックス,0,true));
        }

        for(var コアインデックス = 0;コアインデックス<3;コアインデックス++) {
            var 物理コアインデックス = 8+コアインデックス;
            var 論理先頭 = 16+(コアインデックス*4);
            項目一覧.Add(new CPUコア情報(論理先頭,CPUコア種別.高効率,物理コアインデックス,コアインデックス,0,true));
            項目一覧.Add(new CPUコア情報(論理先頭+1,CPUコア種別.高効率,物理コアインデックス,コアインデックス,0,true));
            項目一覧.Add(new CPUコア情報(論理先頭+2,CPUコア種別.高効率,物理コアインデックス,コアインデックス,0,true));
            項目一覧.Add(new CPUコア情報(論理先頭+3,CPUコア種別.高効率,物理コアインデックス,コアインデックス,0,true));
        }

        return 項目一覧;
    }

    private static 報告行 報告行を作成(string プロジェクト,string メソッド,string 状態文字列,double 平均ナノ秒,long 割り当てバイト数) {
        return new 報告行(プロジェクト,メソッド,string.Empty,string.Empty,状態文字列,平均ナノ秒+" ns",string.Empty,string.Empty,割り当てバイト数+" B",string.Empty,string.Empty,平均ナノ秒,0,0,割り当てバイト数,0,0,string.Empty,string.Empty,string.Empty,string.Empty,string.Empty,string.Empty,null);
    }
}


