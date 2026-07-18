using UI;

namespace Test.Benchmark;

public sealed class ベンチマークタブ進行ロジック {
    [Fact]
    public void 仕様確認01() {
        var ソースタブ = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.System配列);

        var セル一覧 = global::UI.ベンチマークタブ進行元ロジック.選択ケースからセルを作成(
            [
                ケースを作成("System.Array.FLinq","System.Array","Where","Count=4096"),
                ケースを作成("System.Array.ZLinq","System.Array","Where","Count=4096"),
                ケースを作成("System.List.FLinq","System.Collections.Generic.List","Where","Count=4096"),
                ケースを作成("System.Array.FLinq","System.Array","Where","Count=4096"),
            ],
            ソースタブ);

        Assert.Equal(2,セル一覧.Count);
        Assert.All(セル一覧,static セル => Assert.True(セル.選択中か));
        Assert.Contains(セル一覧,static セル => セル.プロジェクト=="System.Array.FLinq");
        Assert.Contains(セル一覧,static セル => セル.プロジェクト=="System.Array.ZLinq");
        Assert.Single(セル一覧.Select(static セル => セル.ペアキー).Distinct(StringComparer.OrdinalIgnoreCase));
        Assert.All(セル一覧,static セル => Assert.False(string.IsNullOrWhiteSpace(セル.ペアキー)));
    }

    [Fact]
    public void 仕様確認02() {
        var 永続化 = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.Seamless永続化);
        var Tsavorite = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.SeamlessTsavorite);
        var 永続化ケース = ケースを作成(
            永続化.右プロジェクト,
            永続化.右アセンブリ名,
            "Benchmark.Persisted.Linq.Enumerable.LinqInt32",
            "Aggregate_func",
            "");
        var Tsavoriteケース = ケースを作成(
            Tsavorite.右プロジェクト,
            Tsavorite.右アセンブリ名,
            "Benchmark.Tsavorite.Linq.Enumerable.LinqInt32",
            "Aggregate_func",
            "");

        {
            var 永続化セル一覧 = global::UI.ベンチマークタブ進行元ロジック.選択ケースからセルを作成([永続化ケース],永続化);
            var Tsavoriteセル一覧 = global::UI.ベンチマークタブ進行元ロジック.選択ケースからセルを作成([永続化ケース],Tsavorite);

            Assert.Single(永続化セル一覧);
            Assert.Empty(Tsavoriteセル一覧);
            var 永続化進行 = global::UI.ベンチマークタブ進行ロジック.進行を計算(
                永続化セル一覧,
                [
                    new ベンチマーク要素測定(
                        永続化.右プロジェクト,
                        "Aggregate_func",
                        "Benchmark.Persisted.Linq.Enumerable.LinqInt32",
                        "",
                        4096,
                        "Completed",
                        2_000_000,
                        2_000_000),
                ],
                永続化.左プロジェクト,
                永続化.右プロジェクト,
                4096,
                1_000_000);
            var Tsavorite進行 = global::UI.ベンチマークタブ進行ロジック.進行を計算(
                Tsavoriteセル一覧,
                [
                    new ベンチマーク要素測定(
                        永続化.右プロジェクト,
                        "Aggregate_func",
                        "Benchmark.Persisted.Linq.Enumerable.LinqInt32",
                        "",
                        4096,
                        "Completed",
                        2_000_000),
                ],
                Tsavorite.左プロジェクト,
                Tsavorite.右プロジェクト,
                4096,
                1_000_000);

            Assert.Equal(1,永続化進行.完了数);
            Assert.Equal(1,永続化進行.合計数);
            Assert.Equal(0,Tsavorite進行.完了数);
            Assert.Equal(0,Tsavorite進行.合計数);
        }
        {
            var 永続化セル一覧 = global::UI.ベンチマークタブ進行元ロジック.選択ケースからセルを作成([Tsavoriteケース],永続化);
            var Tsavoriteセル一覧 = global::UI.ベンチマークタブ進行元ロジック.選択ケースからセルを作成([Tsavoriteケース],Tsavorite);

            Assert.Empty(永続化セル一覧);
            Assert.Single(Tsavoriteセル一覧);
        }
        {
            var 永続化セル一覧 = global::UI.ベンチマークタブ進行元ロジック.選択ケースからセルを作成([永続化ケース,Tsavoriteケース],永続化);
            var Tsavoriteセル一覧 = global::UI.ベンチマークタブ進行元ロジック.選択ケースからセルを作成([永続化ケース,Tsavoriteケース],Tsavorite);

            Assert.Single(永続化セル一覧);
            Assert.Single(Tsavoriteセル一覧);
            Assert.Contains(永続化セル一覧,セル => セル.型完全名=="Benchmark.Persisted.Linq.Enumerable.LinqInt32");
            Assert.Contains(Tsavoriteセル一覧,セル => セル.型完全名=="Benchmark.Tsavorite.Linq.Enumerable.LinqInt32");
        }
    }

    [Fact]
    public void 仕様確認03() {
        var 進行 = global::UI.ベンチマークタブ進行ロジック.進行を計算(
            [
                new ベンチマークタブ進行セル("Where","Left",true),
                new ベンチマークタブ進行セル("Where","Right",false),
                new ベンチマークタブ進行セル("Select","Left",true),
            ],
            [
                new ベンチマーク要素測定("Left","Where","","",4096,"Completed",1_000_000,1_000_000),
                new ベンチマーク要素測定("Right","Where","","",4096,"Completed",100,100),
            ],
            "Left",
            "Right",
            8192,
            1_000_000);

        Assert.Equal(1,進行.完了数);
        Assert.Equal(2,進行.合計数);
        Assert.Equal("1/2",進行.割合文字列);
        Assert.Equal(0.5,進行.割合);
    }

    [Fact]
    public void 仕様確認04() {
        var 進行 = global::UI.ベンチマークタブ進行ロジック.進行を計算(
            [
                new ベンチマークタブ進行セル("Where","Left",true),
                new ベンチマークタブ進行セル("Where","Right",true),
            ],
            [
                new ベンチマーク要素測定("Left","Where","","",4096,"Completed",100,100),
                new ベンチマーク要素測定("Right","Where","","",4096,"Completed",200,200),
            ],
            "Left",
            "Right",
            8192,
            1_000_000);

        Assert.Equal(0,進行.完了数);
        Assert.Equal(2,進行.合計数);
    }

    [Fact]
    public void 仕様確認05() {
        var 進行 = global::UI.ベンチマークタブ進行ロジック.進行を計算(
            [
                new ベンチマークタブ進行セル("A","Left","Type.A","",true,"行-a"),
                new ベンチマークタブ進行セル("A","Right","Type.A","",true,"行-a"),
                new ベンチマークタブ進行セル("B","Left","Type.B","",true,"行-b"),
                new ベンチマークタブ進行セル("B","Right","Type.B","",true,"行-b"),
                new ベンチマークタブ進行セル("C","Left","Type.C","",true,"行-c"),
                new ベンチマークタブ進行セル("C","Right","Type.C","",true,"行-c"),
            ],
            [
                new ベンチマーク要素測定("Left","A","Type.A","",4096,"Completed",2_000_000,2_000_000),
                new ベンチマーク要素測定("Right","A","Type.A","",4096,"Completed",2_000_000,2_000_000),
                new ベンチマーク要素測定("Left","B","Type.B","",4096,"Completed",2_000_000,2_000_000),
                new ベンチマーク要素測定("Right","B","Type.B","",4096,"Completed",2_000_000,2_000_000),
                new ベンチマーク要素測定("Left","C","Type.C","",4096,"Failed",null),
            ],
            "Left",
            "Right",
            4096,
            1_000_000);

        Assert.Equal(5,進行.完了数);
        Assert.Equal(6,進行.合計数);
        Assert.Equal("5/6",進行.割合文字列);
        Assert.NotEqual(1,進行.割合);
    }

    [Fact]
    public void 仕様確認06() {
        var 配列 = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.System配列);
        var 範囲 = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.System範囲);
        var リスト = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.Systemリスト);
        var 選択ケース一覧 = new[]{
            ケースを作成(配列.左プロジェクト,"System.Array","Aggregate_func",""),
            ケースを作成(配列.右プロジェクト,"System.Array","Aggregate_func",""),
            ケースを作成(配列.左プロジェクト,"System.Array","AggregateBy_keySelector",""),
            ケースを作成(配列.右プロジェクト,"System.Array","AggregateBy_keySelector",""),
            ケースを作成(範囲.左プロジェクト,"System.Collections.Generic.IEnumerable","Aggregate_seed_func",""),
            ケースを作成(範囲.右プロジェクト,"System.Collections.Generic.IEnumerable","Aggregate_seed_func",""),
            ケースを作成(範囲.左プロジェクト,"System.Collections.Generic.IEnumerable","Aggregate_seed_func_resultSelector",""),
            ケースを作成(範囲.右プロジェクト,"System.Collections.Generic.IEnumerable","Aggregate_seed_func_resultSelector",""),
            ケースを作成(リスト.左プロジェクト,"System.Collections.Generic.List","All_predicate",""),
            ケースを作成(リスト.右プロジェクト,"System.Collections.Generic.List","All_predicate",""),
            ケースを作成(リスト.左プロジェクト,"System.Collections.Generic.List","Any",""),
            ケースを作成(リスト.右プロジェクト,"System.Collections.Generic.List","Any",""),
        };

        var リストセル一覧 = global::UI.ベンチマークタブ進行元ロジック.選択ケースからセルを作成(選択ケース一覧,リスト);
        var リスト測定一覧 = new[]{
            new ベンチマーク要素測定(リスト.左プロジェクト,"All_predicate","System.Collections.Generic.List","",4096,"Completed",2_000_000,2_000_000),
            new ベンチマーク要素測定(リスト.右プロジェクト,"All_predicate","System.Collections.Generic.List","",4096,"Completed",2_000_000,2_000_000),
        };
        var リスト進行 = global::UI.ベンチマークタブ進行ロジック.進行を計算(
            リストセル一覧,
            リスト測定一覧,
            リスト.左プロジェクト,
            リスト.右プロジェクト,
            4096,
            1_000_000);

        Assert.Equal(2,リスト進行.完了数);
        Assert.Equal(4,リスト進行.合計数);
        Assert.NotEqual(1,リスト進行.割合);

        var 配列セル一覧 = global::UI.ベンチマークタブ進行元ロジック.選択ケースからセルを作成(選択ケース一覧,配列);
        var 配列進行 = global::UI.ベンチマークタブ進行ロジック.進行を計算(
            配列セル一覧,
            [
                new ベンチマーク要素測定(配列.左プロジェクト,"Aggregate_func","System.Array","",4096,"Completed",2_000_000,2_000_000),
                new ベンチマーク要素測定(配列.右プロジェクト,"Aggregate_func","System.Array","",4096,"Completed",2_000_000,2_000_000),
            ],
            配列.左プロジェクト,
            配列.右プロジェクト,
            4096,
            1_000_000);

        Assert.Equal(2,配列進行.完了数);
        Assert.Equal(4,配列進行.合計数);

        var 復元リスト進行 = global::UI.ベンチマークタブ進行ロジック.進行を計算(
            リストセル一覧,
            リスト測定一覧,
            リスト.左プロジェクト,
            リスト.右プロジェクト,
            4096,
            1_000_000);

        Assert.Equal(リスト進行.完了数,復元リスト進行.完了数);
        Assert.Equal(リスト進行.合計数,復元リスト進行.合計数);
        Assert.NotEqual(1,復元リスト進行.割合);
    }

    [Fact]
    public void 仕様確認07() {
        var 配列 = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.System配列);
        var リスト = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.Systemリスト);
        var 選択ケース一覧 = new[]{
            ケースを作成(配列.左プロジェクト,"System.Array","Aggregate_func",""),
            ケースを作成(配列.右プロジェクト,"System.Array","Aggregate_func",""),
            ケースを作成(リスト.左プロジェクト,"System.Collections.Generic.List","All_predicate",""),
            ケースを作成(リスト.右プロジェクト,"System.Collections.Generic.List","All_predicate",""),
        };
        var 保存状態 = new 進行状態 {
            ソースタブ=リスト.タブ,
            ジョブ一覧= [
                完了ジョブを作成(リスト.左プロジェクト,リスト.左アセンブリ名,"System.Collections.Generic.List","All_predicate",4096),
                完了ジョブを作成(リスト.右プロジェクト,リスト.右アセンブリ名,"System.Collections.Generic.List","All_predicate",4096),
            ],
        };
        var リストセル一覧 = global::UI.ベンチマークタブ進行元ロジック.選択ケースからセルを作成(選択ケース一覧,リスト);
        var リスト測定一覧 = 保存状態.ジョブ一覧
            .Where(ジョブ => ジョブ.ソースタブ==リスト.タブ)
            .Select(static ジョブ => new ベンチマーク結果項目(ジョブ))
            .Select(static 項目 => new ベンチマーク要素測定(
                項目.プロジェクト,
                項目.メソッド,
                項目.型完全名,
                項目.引数キー,
                項目.軸数,
                項目.状態,
                項目.平均ナノ秒,
                項目.合計ナノ秒))
            .ToArray();

        var 進行 = global::UI.ベンチマークタブ進行ロジック.進行を計算(
            リストセル一覧,
            リスト測定一覧,
            リスト.左プロジェクト,
            リスト.右プロジェクト,
            4096,
            1_000_000);

        Assert.Equal(2,進行.完了数);
        Assert.Equal(2,進行.合計数);
        Assert.Equal(1,進行.割合);
    }

    [Fact]
    public void 仕様確認08() {
        var 進行 = global::UI.ベンチマークタブ進行ロジック.進行を計算(
            [
                new ベンチマークタブ進行セル("Where","Left",true),
            ],
            [
                new ベンチマーク要素測定("Left","Where",4096,"Failed",null),
            ],
            "Left",
            "Right",
            8192,
            1_000_000);

        Assert.Equal(1,進行.完了数);
        Assert.Equal(1,進行.合計数);
    }

    [Fact]
    public void 仕様確認09() {
        var 進行 = global::UI.ベンチマークタブ進行ロジック.進行を計算(
            [
                new ベンチマークタブ進行セル("Where","Left",true),
                new ベンチマークタブ進行セル("Where","Right",true),
            ],
            [
                new ベンチマーク要素測定("Left","Where","","",4096,"Completed",2_000_000,2_000_000),
                new ベンチマーク要素測定("Right","Where","","",4096,"Completed",100,100),
            ],
            "Left",
            "Right",
            8192,
            1_000_000);

        Assert.Equal(2,進行.完了数);
        Assert.Equal(2,進行.合計数);
    }

    [Fact]
    public void 仕様確認10() {
        var 進行 = global::UI.ベンチマークタブ進行ロジック.進行を計算(
            [
                new ベンチマークタブ進行セル("Any","Left","System.Array.FLinq","",true,"行-1"),
                new ベンチマークタブ進行セル("Any","Right","System.Array.ZLinq","",true,"行-1"),
            ],
            [
                new ベンチマーク要素測定("Left","Any","System.Array.FLinq","",4096,"Completed",100,100),
                new ベンチマーク要素測定("Right","Any","System.Array.ZLinq","",4096,"Completed",2_000_000,2_000_000),
            ],
            "Left",
            "Right",
            4096,
            1_000_000);

        Assert.Equal(2,進行.完了数);
        Assert.Equal(2,進行.合計数);
    }

    [Fact]
    public void 仕様確認11() {
        var 進行 = global::UI.ベンチマークタブ進行ロジック.進行を計算(
            [
                new ベンチマークタブ進行セル("Any","Left","System.Array.FLinq","",true,"行-1"),
                new ベンチマークタブ進行セル("Any","Right","System.Array.ZLinq","",true,"行-2"),
            ],
            [
                new ベンチマーク要素測定("Left","Any","System.Array.FLinq","",4096,"Completed",100,100),
                new ベンチマーク要素測定("Right","Any","System.Array.ZLinq","",4096,"Completed",2_000_000,2_000_000),
            ],
            "Left",
            "Right",
            4096,
            1_000_000);

        Assert.Equal(1,進行.完了数);
        Assert.Equal(2,進行.合計数);
    }

    [Fact]
    public void 仕様確認12() {
        var 進行 = global::UI.ベンチマークタブ進行ロジック.進行を計算(
            [
                new ベンチマークタブ進行セル("Add","Scalable.Persisted.List","Benchmark.Scalable.Persisted.Collections.List.ListString",true),
            ],
            [
                new ベンチマーク要素測定("Scalable.Persisted.List","Add","Benchmark.Scalable.Persisted.Collections.List.ListInt32",4096,"Completed",100),
            ],
            "Scalable.Persisted.List",
            "Scalable.Persisted.List.Previous",
            8192,
            1_000_000);

        Assert.Equal(0,進行.完了数);
        Assert.Equal(1,進行.合計数);
    }

    [Fact]
    public void 仕様確認13() {
        var 進行 = global::UI.ベンチマークタブ進行ロジック.進行を計算(
            [
                new ベンチマークタブ進行セル("Add","Left","Type","Count=2",true),
                new ベンチマークタブ進行セル("Add","Right","Type","Count=2",true),
            ],
            [
                new ベンチマーク要素測定("Left","Add","Type","Count=1",4096,"Completed",2_000_000,2_000_000),
                new ベンチマーク要素測定("Right","Add","Type","Count=1",4096,"Completed",100,100),
            ],
            "Left",
            "Right",
            4096,
            1_000_000);

        Assert.Equal(0,進行.完了数);
        Assert.Equal(2,進行.合計数);
    }

    [Fact]
    public void 全標準とScalableタブの選択ケースはタブ別に進捗へ固定できる() {
        var 定義一覧=ベンチマークソースタブ一覧.標準全て
            .Concat(ベンチマークソースタブ一覧.スケーラブル全て)
            .ToArray();
        var ケース一覧=定義一覧
            .SelectMany(定義=>定義.プロジェクト順.Select(プロジェクト=>new ベンチマークケース(
                プロジェクト,
                定義.アセンブリ名を取得(プロジェクト),
                型完全名を作成(定義,プロジェクト),
                "Execute",
                "")))
            .ToArray();

        foreach(var 定義 in 定義一覧) {
            var セル一覧=ベンチマークタブ進行元ロジック.選択ケースからセルを作成(ケース一覧,定義);

            Assert.Equal(定義.プロジェクト順.Count,セル一覧.Count);
            Assert.All(セル一覧,セル=>Assert.Contains(セル.プロジェクト,定義.プロジェクト順));
            Assert.All(セル一覧,static セル=>Assert.True(セル.選択中か));
        }
    }

    private static string 型完全名を作成(ベンチマークソースタブ定義 定義,string プロジェクト) {
        var 接頭辞=プロジェクト==定義.左プロジェクト
            ? 定義.左型接頭辞
            : プロジェクト==定義.中央プロジェクト
                ? 定義.中央型接頭辞
                : 定義.右型接頭辞;
        return string.IsNullOrWhiteSpace(接頭辞) ? "Test.Sample" : 接頭辞+"Sample";
    }
    private static ベンチマークケース ケースを作成(string プロジェクト,string 型,string メソッド,string 引数キー) =>
        new(プロジェクト,アセンブリ名を解決(プロジェクト),型,メソッド,引数キー,[]);

    private static ベンチマークケース ケースを作成(string プロジェクト,string アセンブリ名,string 型,string メソッド,string 引数キー) =>
        new(プロジェクト,アセンブリ名,型,メソッド,引数キー,[]);

    private static ベンチマークジョブ 完了ジョブを作成(string プロジェクト,string アセンブリ名,string 型完全名,string メソッド,int 要素数) =>
        new() {
            プロジェクト=プロジェクト,
            アセンブリ名=アセンブリ名,
            型完全名=型完全名,
            メソッド名=メソッド,
            ソースタブ=ベンチマークソースタブ一覧.プロジェクトとアセンブリから取得を試みる(プロジェクト,アセンブリ名)?.タブ??ベンチマークソースタブ.System配列,
            要素数=要素数,
            ループ回数=1,
            状態=ジョブ状態.完了,
            平均ナノ秒=2_000_000,
        };

    private static string アセンブリ名を解決(string プロジェクト) =>
        ベンチマークソースタブ一覧.プロジェクトから取得を試みる(プロジェクト)?.アセンブリ名を取得(プロジェクト)??"Test.Assembly";
}

