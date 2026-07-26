using UI;

namespace Test.Benchmark;

public sealed class ベンチマーク行グループ化ロジック {
    [Fact]
    public void 仕様確認01() {
        var ソースタブ = 比較ソースタブを作成();
        var 行一覧 = global::UI.ベンチマーク行グループ化ロジック.行を作成(
            [
                ケースを作成(ソースタブ.左プロジェクト,"Left.Assembly","Left.Route","Where"),
                ケースを作成(ソースタブ.右プロジェクト,"Right.Assembly","Right.Route","Where"),
            ],
            ソースタブ);

        var 行 = Assert.Single(行一覧);
        Assert.Equal("Where",行.メソッド名);
        Assert.True(行.プロジェクト別ケース.ContainsKey(ソースタブ.左プロジェクト));
        Assert.True(行.プロジェクト別ケース.ContainsKey(ソースタブ.右プロジェクト));
    }

    [Fact]
    public void 仕様確認02() {
        var ソースタブ = 比較ソースタブを作成();
        var 行一覧 = global::UI.ベンチマーク行グループ化ロジック.行を作成(
            [
                ケースを作成(ソースタブ.左プロジェクト,"Left.Assembly","Left.TypeA","Where"),
                ケースを作成(ソースタブ.左プロジェクト,"Left.Assembly","Left.TypeB","Where"),
                ケースを作成(ソースタブ.右プロジェクト,"Right.Assembly","Right.TypeA","Where"),
                ケースを作成(ソースタブ.右プロジェクト,"Right.Assembly","Right.TypeB","Where"),
            ],
            ソースタブ);

        Assert.Equal(2,行一覧.Count);
        Assert.NotEqual(行一覧[0].行キー,行一覧[1].行キー);
        Assert.All(行一覧,行 => Assert.Equal("Where",行.メソッド名));
        Assert.All(行一覧,行 => Assert.Equal("Where",行.ベンチマーク名));
    }

    [Fact]
    public void 仕様確認03() {
        var ソースタブ = 比較ソースタブを作成();
        var 行一覧 = global::UI.ベンチマーク行グループ化ロジック.行を作成(
            [
                ケースを作成(ソースタブ.左プロジェクト,"Left.Assembly","Left.TypeA","Add","Count=1",[new ベンチマーク引数("Count","1")]),
                ケースを作成(ソースタブ.左プロジェクト,"Left.Assembly","Left.TypeA","Add","Count=2",[new ベンチマーク引数("Count","2")]),
                ケースを作成(ソースタブ.右プロジェクト,"Right.Assembly","Right.TypeA","Add","Count=1",[new ベンチマーク引数("Count","1")]),
                ケースを作成(ソースタブ.右プロジェクト,"Right.Assembly","Right.TypeA","Add","Count=2",[new ベンチマーク引数("Count","2")]),
            ],
            ソースタブ);

        Assert.Equal(2,行一覧.Count);
        Assert.NotEqual(行一覧[0].行キー,行一覧[1].行キー);
        Assert.Contains(行一覧,行 => 行.行キー.Contains("Count=1",StringComparison.Ordinal));
        Assert.Contains(行一覧,行 => 行.行キー.Contains("Count=2",StringComparison.Ordinal));
        Assert.All(行一覧,行 => Assert.Equal("Add",行.メソッド名));
    }

    [Fact]
    public void 仕様確認04() {
        var ソースタブ = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.スケーラブル永続化リスト);
        var 行一覧 = global::UI.ベンチマーク行グループ化ロジック.行を作成(
            [
                ケースを作成(ソースタブ.左プロジェクト,"Benchmark.Scalable.Persisted.Collections","Benchmark.Scalable.Persisted.Collections.List.ListString","Add"),
                ケースを作成(ソースタブ.左プロジェクト,"Benchmark.Scalable.Persisted.Collections","Benchmark.Scalable.Persisted.Collections.List.ListInt32","Add"),
                ケースを作成(ソースタブ.左プロジェクト,"Benchmark.Scalable.Persisted.Collections","Benchmark.Scalable.Persisted.Collections.List.ListBoolean","Add"),
            ],
            ソースタブ);

        Assert.Contains(行一覧,行 => 行.ベンチマーク名=="<string>.Add");
        Assert.Contains(行一覧,行 => 行.ベンチマーク名=="<int>.Add");
        Assert.Contains(行一覧,行 => 行.ベンチマーク名=="<System.Boolean>.Add");
        Assert.All(行一覧,行 => Assert.Equal("Add",行.メソッド名));
    }

    [Fact]
    public void 仕様確認05() {
        var ソースタブ = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.スケーラブルTsavorite辞書);
        var 行一覧 = global::UI.ベンチマーク行グループ化ロジック.行を作成(
            [
                ケースを作成(ソースタブ.左プロジェクト,"Benchmark.Scalable.Tsavorite.Collections","Benchmark.Scalable.Tsavorite.Collections.Dictionary.DictionaryStringInt32","Add"),
                ケースを作成(ソースタブ.左プロジェクト,"Benchmark.Scalable.Tsavorite.Collections","Benchmark.Scalable.Tsavorite.Collections.Dictionary.DictionaryManagedStructManagedStruct","Add"),
            ],
            ソースタブ);

        Assert.Contains(行一覧,行 => 行.ベンチマーク名=="<string,int>.Add");
        Assert.Contains(行一覧,行 => 行.ベンチマーク名=="<ManagedStruct,ManagedStruct>.Add");
        Assert.All(行一覧,行 => Assert.Equal("Add",行.メソッド名));
    }

    [Fact]
    public void 仕様確認06() {
        var ソースタブ = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.Seamless永続化);
        var 行一覧 = global::UI.ベンチマーク行グループ化ロジック.行を作成(
            [
                ケースを作成(ソースタブ.左プロジェクト,"Benchmark.Persisted.Linq.Enumerable.FLinq","Benchmark.Persisted.Linq.Enumerable.FLinqInt32","Aggregate_func"),
                ケースを作成(ソースタブ.左プロジェクト,"Benchmark.Persisted.Linq.Enumerable.FLinq","Benchmark.Persisted.Linq.Enumerable.FLinqString","Aggregate_func"),
                ケースを作成(ソースタブ.右プロジェクト,"Benchmark.Persisted.Linq.Enumerable.Linq","Benchmark.Persisted.Linq.Enumerable.LinqInt32","Aggregate_func"),
                ケースを作成(ソースタブ.右プロジェクト,"Benchmark.Persisted.Linq.Enumerable.Linq","Benchmark.Persisted.Linq.Enumerable.LinqString","Aggregate_func"),
            ],
            ソースタブ);

        Assert.Contains(行一覧,行 => 行.ベンチマーク名=="<int>.Aggregate_func");
        Assert.Contains(行一覧,行 => 行.ベンチマーク名=="<string>.Aggregate_func");
        Assert.All(行一覧,行 => Assert.Equal("Aggregate_func",行.メソッド名));
    }

    [Fact]
    public void 仕様確認07() {
        var ソースタブ = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.SeamlessTsavorite);
        var 行一覧 = global::UI.ベンチマーク行グループ化ロジック.行を作成(
            [
                ケースを作成(ソースタブ.左プロジェクト,"Benchmark.Tsavorite.Linq.Enumerable.FLinq","Benchmark.Tsavorite.Linq.Enumerable.FLinqInt32","All_predicate"),
                ケースを作成(ソースタブ.左プロジェクト,"Benchmark.Tsavorite.Linq.Enumerable.FLinq","Benchmark.Tsavorite.Linq.Enumerable.FLinqString","All_predicate"),
                ケースを作成(ソースタブ.右プロジェクト,"Benchmark.Tsavorite.Linq.Enumerable.Linq","Benchmark.Tsavorite.Linq.Enumerable.LinqInt32","All_predicate"),
                ケースを作成(ソースタブ.右プロジェクト,"Benchmark.Tsavorite.Linq.Enumerable.Linq","Benchmark.Tsavorite.Linq.Enumerable.LinqString","All_predicate"),
            ],
            ソースタブ);

        Assert.Contains(行一覧,行 => 行.ベンチマーク名=="<int>.All_predicate");
        Assert.Contains(行一覧,行 => 行.ベンチマーク名=="<string>.All_predicate");
        Assert.All(行一覧,行 => Assert.Equal("All_predicate",行.メソッド名));
    }

    [Theory]
    [InlineData(ベンチマークソースタブ.Seamless永続化,"Benchmark.Persisted.Linq.Enumerable","ValueLinq")]
    [InlineData(ベンチマークソースタブ.System永続化,"Benchmark.Persisted.Linq.Enumerable","Linq")]
    public void FLinqと右列は同じ型行へ結合される(ベンチマークソースタブ タブ,string 名前空間,string 右方式) {
        var ソースタブ=ベンチマークソースタブ一覧.取得(タブ);
        foreach(var メソッド名 in new[]{"Count","Count_predicate"}) {
            var 行一覧=global::UI.ベンチマーク行グループ化ロジック.行を作成(
                [
                    ケースを作成(ソースタブ.左プロジェクト,ソースタブ.左アセンブリ名,名前空間+".FLinqInt32",メソッド名),
                    ケースを作成(ソースタブ.右プロジェクト,ソースタブ.右アセンブリ名,名前空間+"."+右方式+"Int32",メソッド名),
                ],
                ソースタブ);

            var 行=Assert.Single(行一覧);
            var 期待名=タブ==ベンチマークソースタブ.System永続化 ? メソッド名 : "<int>."+メソッド名;
            Assert.Equal(期待名,行.ベンチマーク名);
            Assert.Equal(ソースタブ.プロジェクト順.Order(StringComparer.OrdinalIgnoreCase),行.プロジェクト別ケース.Keys.Order(StringComparer.OrdinalIgnoreCase));
        }
    }
    [Fact]
    public void 仕様確認08() {
        var ソースタブ = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.Seamless永続化);
        var 別タブ = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.SeamlessTsavorite);
        var 行一覧 = global::UI.ベンチマーク行グループ化ロジック.行を作成(
            [
                ケースを作成(ソースタブ.左プロジェクト,ソースタブ.左アセンブリ名,"Benchmark.Persisted.Linq.Enumerable.FLinqInt32","AggregateBy_keySelector"),
                ケースを作成(ソースタブ.左プロジェクト,ソースタブ.左アセンブリ名,"Benchmark.Persisted.Linq.Enumerable.FLinqString","AggregateBy_keySelector"),
                ケースを作成(ソースタブ.右プロジェクト,ソースタブ.右アセンブリ名,"Benchmark.Persisted.Linq.Enumerable.LinqInt32","AggregateBy_keySelector"),
                ケースを作成(ソースタブ.右プロジェクト,ソースタブ.右アセンブリ名,"Benchmark.Persisted.Linq.Enumerable.LinqString","AggregateBy_keySelector"),
                ケースを作成(ソースタブ.右プロジェクト,別タブ.右アセンブリ名,"Benchmark.Tsavorite.Linq.Enumerable.LinqInt32","AggregateBy_keySelector"),
                ケースを作成(ソースタブ.右プロジェクト,別タブ.右アセンブリ名,"Benchmark.Tsavorite.Linq.Enumerable.LinqString","AggregateBy_keySelector"),
            ],
            ソースタブ);

        Assert.Equal(2,行一覧.Count);
        Assert.Contains(行一覧,行 => 行.ベンチマーク名=="<int>.AggregateBy_keySelector");
        Assert.Contains(行一覧,行 => 行.ベンチマーク名=="<string>.AggregateBy_keySelector");
        Assert.All(行一覧,行 => Assert.Equal(ソースタブ.右アセンブリ名,行.プロジェクト別ケース[ソースタブ.右プロジェクト].アセンブリ名));
    }

    [Fact]
    public void 仕様確認09() {
        var ソースタブ = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.SeamlessTsavorite);
        var 別タブ = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.Seamless永続化);
        var 行一覧 = global::UI.ベンチマーク行グループ化ロジック.行を作成(
            [
                ケースを作成(ソースタブ.左プロジェクト,ソースタブ.左アセンブリ名,"Benchmark.Tsavorite.Linq.Enumerable.FLinqInt32","AggregateBy_keySelector"),
                ケースを作成(ソースタブ.左プロジェクト,ソースタブ.左アセンブリ名,"Benchmark.Tsavorite.Linq.Enumerable.FLinqString","AggregateBy_keySelector"),
                ケースを作成(ソースタブ.右プロジェクト,別タブ.右アセンブリ名,"Benchmark.Persisted.Linq.Enumerable.LinqInt32","AggregateBy_keySelector"),
                ケースを作成(ソースタブ.右プロジェクト,別タブ.右アセンブリ名,"Benchmark.Persisted.Linq.Enumerable.LinqString","AggregateBy_keySelector"),
                ケースを作成(ソースタブ.右プロジェクト,ソースタブ.右アセンブリ名,"Benchmark.Tsavorite.Linq.Enumerable.LinqInt32","AggregateBy_keySelector"),
                ケースを作成(ソースタブ.右プロジェクト,ソースタブ.右アセンブリ名,"Benchmark.Tsavorite.Linq.Enumerable.LinqString","AggregateBy_keySelector"),
            ],
            ソースタブ);

        Assert.Equal(2,行一覧.Count);
        Assert.Contains(行一覧,行 => 行.ベンチマーク名=="<int>.AggregateBy_keySelector");
        Assert.Contains(行一覧,行 => 行.ベンチマーク名=="<string>.AggregateBy_keySelector");
        Assert.All(行一覧,行 => Assert.Equal(ソースタブ.右アセンブリ名,行.プロジェクト別ケース[ソースタブ.右プロジェクト].アセンブリ名));
    }

    [Fact]
    public void 仕様確認10() {
        var 永続化 = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.Seamless永続化);
        var Tsavorite = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.SeamlessTsavorite);
        var 行一覧 = global::UI.ベンチマーク行グループ化ロジック.行を作成(
            [
                ケースを作成(永続化.左プロジェクト,永続化.左アセンブリ名,"Benchmark.Persisted.Linq.Enumerable.FLinqClassInt32","AggregateBy_keySelector","Persisted<ClassInt32>"),
                ケースを作成(永続化.右プロジェクト,永続化.右アセンブリ名,"Benchmark.Persisted.Linq.Enumerable.LinqClassInt32","AggregateBy_keySelector","Persisted<ClassInt32>"),
                ケースを作成(Tsavorite.右プロジェクト,Tsavorite.右アセンブリ名,"Benchmark.Tsavorite.Linq.Enumerable.LinqClassInt32","AggregateBy_keySelector","Tsavorite<ClassInt32>"),
            ],
            永続化);

        var 行 = Assert.Single(行一覧);
        Assert.True(行.プロジェクト別ケース.ContainsKey(永続化.左プロジェクト));
        Assert.True(行.プロジェクト別ケース.ContainsKey(永続化.右プロジェクト));
        Assert.All(行.プロジェクト別ケース.Values,ベンチマーク => Assert.True(永続化.ケースを含むか(ベンチマーク)));
    }

    [Fact]
    public void 仕様確認11() {
        var 永続化 = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.Seamless永続化);
        var 行一覧 = global::UI.ベンチマーク行グループ化ロジック.行を作成(
            [
                ケースを作成(永続化.左プロジェクト,永続化.左アセンブリ名,"Benchmark.Persisted.Linq.Enumerable.FLinqClassInt32","AggregateBy_keySelector","Persisted<ClassInt32>"),
            ],
            永続化);

        var 行 = Assert.Single(行一覧);
        Assert.True(行.プロジェクト別ケース.ContainsKey(永続化.左プロジェクト));
        Assert.False(行.プロジェクト別ケース.ContainsKey(永続化.右プロジェクト));
    }

    [Fact]
    public void 仕様確認12() {
        var ソースタブ = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.System配列);
        var 行一覧 = global::UI.ベンチマーク行グループ化ロジック.行を作成(
            [
                ケースを作成(ソースタブ.左プロジェクト,"Benchmark.System.Linq.Enumerable.FLinq","Benchmark.System.Linq.Enumerable.FLinqInt32","Any"),
                ケースを作成(ソースタブ.左プロジェクト,"Benchmark.System.Linq.Enumerable.FLinq","Benchmark.System.Linq.Enumerable.FLinqString","Any"),
                ケースを作成(ソースタブ.右プロジェクト,"Benchmark.System.Linq.Enumerable.ZLinq","Benchmark.System.Linq.Enumerable.ZLinqInt32","Any"),
                ケースを作成(ソースタブ.右プロジェクト,"Benchmark.System.Linq.Enumerable.ZLinq","Benchmark.System.Linq.Enumerable.ZLinqString","Any"),
            ],
            ソースタブ);

        Assert.Equal(2,行一覧.Count);
        Assert.Contains(行一覧,行 => 行.ベンチマーク名=="<int>.Any");
        Assert.Contains(行一覧,行 => 行.ベンチマーク名=="<string>.Any");
        Assert.All(行一覧,行 => Assert.Equal("Any",行.メソッド名));
    }

    [Fact]
    public void 仕様確認13() {
        var ソースタブ = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.System配列);
        var 行一覧 = global::UI.ベンチマーク行グループ化ロジック.行を作成(
            [
                ケースを作成(ソースタブ.左プロジェクト,ソースタブ.左アセンブリ名,"Benchmark.System.Linq.Enumerable.FLinqInt32","Aggregate_func"),
                ケースを作成(ソースタブ.右プロジェクト,ソースタブ.右アセンブリ名,"Benchmark.System.Linq.Enumerable.ZLinqInt32","Aggregate_func"),
            ],
            ソースタブ);

        var 行 = Assert.Single(行一覧);

        Assert.True(行.プロジェクト別ケース.ContainsKey(ソースタブ.左プロジェクト));
        Assert.True(行.プロジェクト別ケース.ContainsKey(ソースタブ.右プロジェクト));
        Assert.Equal(ソースタブ.右アセンブリ名,行.プロジェクト別ケース[ソースタブ.右プロジェクト].アセンブリ名);
    }

    [Fact]
    public void 仕様確認14() {
        var ソースタブ = ベンチマークソースタブ一覧.既定;
        var 行一覧 = ベンチマーク比較フォーマッター.比較行を構築(
            [
                報告行を作成(ソースタブ.左プロジェクト,"Where","Left.Route",10),
                報告行を作成(ソースタブ.右プロジェクト,"Where","Right.Route",20),
            ],
            ソースタブ);

        var 行 = Assert.Single(行一覧);
        Assert.Equal("Where",行.ベンチマーク);
        Assert.Equal(string.Empty,行.型完全名);
    }

    [Fact]
    public void 仕様確認15() {
        var ソースタブ = ベンチマークソースタブ一覧.既定;
        var 行一覧 = ベンチマーク比較フォーマッター.比較行を構築(
            [
                報告行を作成(ソースタブ.左プロジェクト,"Where","Left.TypeA",10),
                報告行を作成(ソースタブ.左プロジェクト,"Where","Left.TypeB",11),
                報告行を作成(ソースタブ.右プロジェクト,"Where","Right.TypeA",20),
                報告行を作成(ソースタブ.右プロジェクト,"Where","Right.TypeB",21),
            ],
            ソースタブ);

        Assert.Equal(2,行一覧.Count);
        Assert.Contains(行一覧,行 => 行.型完全名=="TypeA");
        Assert.Contains(行一覧,行 => 行.型完全名=="TypeB");
    }

    [Fact]
    public void 仕様確認16() {
        var ソースタブ = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.スケーラブル永続化リスト);
        var 行一覧 = ベンチマーク比較フォーマッター.比較行を構築(
            [
                報告行を作成(ソースタブ.左プロジェクト,"Add","Benchmark.Scalable.Persisted.Collections.List.ListInt32",10),
            ],
            ソースタブ);

        var 行 = Assert.Single(行一覧);
        Assert.Equal("<int>.Add",行.ベンチマーク);
        Assert.Equal("ListInt32",行.型完全名);
    }

    [Fact]
    public void 仕様確認17() {
        var ソースタブ = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.スケーラブルTsavoriteセット);
        var 行一覧 = ベンチマーク比較フォーマッター.比較行を構築(
            [
                報告行を作成(ソースタブ.左プロジェクト,"Add","Benchmark.Scalable.Tsavorite.Collections.Set.SetInt32",20),
            ],
            ソースタブ,
            131072);

        var 行 = Assert.Single(行一覧);
        Assert.Equal("<int>.Add",行.ベンチマーク);
        Assert.Equal("Add",行.メソッド名);
        Assert.Equal("SetInt32",行.型完全名);
    }

    [Fact]
    public void 仕様確認18() {
        var ソースタブ = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.スケーラブルTsavorite辞書);
        var 行一覧 = ベンチマーク比較フォーマッター.比較行を構築(
            [
                報告行を作成(ソースタブ.左プロジェクト,"Add","Benchmark.Scalable.Tsavorite.Collections.Dictionary.DictionaryStringInt32",20),
            ],
            ソースタブ,
            131072);

        var 行 = Assert.Single(行一覧);

        Assert.Equal("<string,int>.Add",行.ベンチマーク);
        Assert.Equal("Add",行.メソッド名);
        Assert.Equal("DictionaryStringInt32",行.型完全名);
    }

    [Fact]
    public void 仕様確認19() {
        var ソースタブ = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.Seamless永続化);
        var 行一覧 = ベンチマーク比較フォーマッター.比較行を構築(
            [
                報告行を作成(ソースタブ.左プロジェクト,"Aggregate_func","Benchmark.Persisted.Linq.Enumerable.FLinqInt32",20),
            ],
            ソースタブ);

        var 行 = Assert.Single(行一覧);

        Assert.Equal("<int>.Aggregate_func",行.ベンチマーク);
        Assert.Equal("Aggregate_func",行.メソッド名);
        Assert.Equal("int",行.型完全名);
    }

    [Fact]
    public void 仕様確認20() {
        var ソースタブ = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.System配列);
        var 行一覧 = ベンチマーク比較フォーマッター.比較行を構築(
            [
                報告行を作成(ソースタブ.左プロジェクト,"Any","Benchmark.System.Linq.Enumerable.FLinqInt32",10),
                報告行を作成(ソースタブ.右プロジェクト,"Any","Benchmark.System.Linq.Enumerable.ZLinqInt32",20),
                報告行を作成(ソースタブ.左プロジェクト,"Any","Benchmark.System.Linq.Enumerable.FLinqString",30),
                報告行を作成(ソースタブ.右プロジェクト,"Any","Benchmark.System.Linq.Enumerable.ZLinqString",40),
            ],
            ソースタブ);

        Assert.Equal(2,行一覧.Count);
        var 整数行 = Assert.Single(行一覧,行 => 行.型完全名=="int");
        var 文字列行 = Assert.Single(行一覧,行 => 行.型完全名=="string");

        Assert.Equal("<int>.Any",整数行.ベンチマーク);
        Assert.Equal(10,整数行.プロジェクトを取得(ソースタブ.左プロジェクト).時間ナノ秒);
        Assert.Equal(20,整数行.プロジェクトを取得(ソースタブ.右プロジェクト).時間ナノ秒);
        Assert.Equal("<string>.Any",文字列行.ベンチマーク);
        Assert.Equal(30,文字列行.プロジェクトを取得(ソースタブ.左プロジェクト).時間ナノ秒);
        Assert.Equal(40,文字列行.プロジェクトを取得(ソースタブ.右プロジェクト).時間ナノ秒);
    }

    [Fact]
    public void 仕様確認21() {
        var ソースタブ = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.System配列);
        var 行一覧 = ベンチマーク比較フォーマッター.比較行を構築(
            [
                報告行を作成(ソースタブ.左プロジェクト,"Any","Benchmark.System.Linq.Enumerable.FLinq",10),
                報告行を作成(ソースタブ.右プロジェクト,"Any","Benchmark.System.Linq.Enumerable.ZLinq",20),
            ],
            ソースタブ);

        var 行 = Assert.Single(行一覧);

        Assert.Equal("<int>.Any",行.ベンチマーク);
        Assert.Equal("int",行.型完全名);
        Assert.Equal(10,行.プロジェクトを取得(ソースタブ.左プロジェクト).時間ナノ秒);
        Assert.Equal(20,行.プロジェクトを取得(ソースタブ.右プロジェクト).時間ナノ秒);
    }

    [Fact]
    public void 仕様確認22() {
        var ソースタブ = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.スケーラブルTsavoriteセット);
        var 引数一覧 = new ベンチマーク引数[] {
            new("Count","131072"),
            new("Index","131072"),
        };
        var ケース一覧 = new ベンチマーク比較行一致ケース[] {
            new("Benchmark.Scalable.Tsavorite.Collections.Set.SetInt32",ベンチマーク引数フォーマッター.キーを作成(引数一覧)),
        };
        var 完全一致 = new ベンチマーク比較行(
            "<int>.Add",
            ソースタブ,
            プロジェクト測定値.測定から作成(10,0),
            プロジェクト測定値.欠測) {
            メソッド名="Add",
            型完全名="SetInt32",
            引数キー=ベンチマーク引数フォーマッター.キーを作成(引数一覧),
            引数一覧=引数一覧,
        };
        var 別引数 = 完全一致 with {
            引数キー="Count=262144,Index=131072",
            引数一覧= [
                new ベンチマーク引数("Count","262144"),
                new ベンチマーク引数("Index","131072"),
            ],
        };
        var 空引数 = 完全一致 with {
            引数キー=string.Empty,
            引数一覧= [],
        };

        Assert.True(ベンチマーク比較行一致ロジック.一致か("Add","<int>.Add",ソースタブ.タブ,ケース一覧,完全一致));
        Assert.False(ベンチマーク比較行一致ロジック.一致か("Add","<int>.Add",ソースタブ.タブ,ケース一覧,別引数));
        Assert.False(ベンチマーク比較行一致ロジック.一致か("Add","<int>.Add",ソースタブ.タブ,ケース一覧,空引数));
    }

    [Fact]
    public void 仕様確認23() {
        {
            var ソースタブ = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.Seamless永続化);
            var ケース一覧 = new ベンチマーク比較行一致ケース[] {
                new("Benchmark.Persisted.Linq.Enumerable.FLinqInt32",string.Empty),
            };
            var 完全一致 = new ベンチマーク比較行(
                "<int>.Aggregate_func",
                ソースタブ,
                プロジェクト測定値.測定から作成(10,0),
                プロジェクト測定値.欠測) {
                メソッド名="Aggregate_func",
                型完全名="int",
            };
            var 別型 = 完全一致 with {
                ベンチマーク="<string>.Aggregate_func",
                型完全名="string",
            };

            Assert.True(ベンチマーク比較行一致ロジック.一致か("Aggregate_func","<int>.Aggregate_func",ソースタブ.タブ,ケース一覧,完全一致));
            Assert.False(ベンチマーク比較行一致ロジック.一致か("Aggregate_func","<int>.Aggregate_func",ソースタブ.タブ,ケース一覧,別型));
        }
        {
            var ソースタブ = ベンチマークソースタブ一覧.取得(ベンチマークソースタブ.SeamlessTsavorite);
            var ケース一覧 = new ベンチマーク比較行一致ケース[] {
                new("Benchmark.Tsavorite.Linq.Enumerable.FLinqInt32",string.Empty),
            };
            var 完全一致 = new ベンチマーク比較行(
                "<int>.All_predicate",
                ソースタブ,
                プロジェクト測定値.測定から作成(10,0),
                プロジェクト測定値.欠測) {
                メソッド名="All_predicate",
                型完全名="int",
            };
            var 別型 = 完全一致 with {
                ベンチマーク="<string>.All_predicate",
                型完全名="string",
            };

            Assert.True(ベンチマーク比較行一致ロジック.一致か("All_predicate","<int>.All_predicate",ソースタブ.タブ,ケース一覧,完全一致));
            Assert.False(ベンチマーク比較行一致ロジック.一致か("All_predicate","<int>.All_predicate",ソースタブ.タブ,ケース一覧,別型));
        }
    }

    [Fact]
    public void 仕様確認24() {
        var 行 = default(ベンチマーク比較行);

        var 指標一覧 = 行.プロジェクトを取得(ベンチマークソースタブ一覧.既定.左プロジェクト);

        Assert.False(指標一覧.測定値ありか);
    }

    [Fact]
    public void 仕様確認25() {
        Assert.Equal("int",スケーラブルベンチマーク名フォーマッター.ベンチマーク型表示を作成("Benchmark.Persisted.Linq.Enumerable.FLinqInt32"));
        Assert.Equal("string",スケーラブルベンチマーク名フォーマッター.ベンチマーク型表示を作成("Benchmark.Persisted.Linq.Enumerable.LinqString"));
        Assert.Equal("int",スケーラブルベンチマーク名フォーマッター.ベンチマーク型表示を作成("Benchmark.Tsavorite.Linq.Enumerable.FLinqInt32"));
        Assert.Equal("string",スケーラブルベンチマーク名フォーマッター.ベンチマーク型表示を作成("Benchmark.Tsavorite.Linq.Enumerable.LinqString"));
        Assert.Equal("int",スケーラブルベンチマーク名フォーマッター.ベンチマーク型表示を作成("Benchmark.System.Linq.Enumerable.FLinqInt32"));
        Assert.Equal("string",スケーラブルベンチマーク名フォーマッター.ベンチマーク型表示を作成("Benchmark.System.Linq.Enumerable.ZLinqString"));
        Assert.Equal("int",スケーラブルベンチマーク名フォーマッター.ベンチマーク型表示を作成("Benchmark.Optimization.All.ArrayInt32"));
        Assert.Equal("ClassInt32",スケーラブルベンチマーク名フォーマッター.ベンチマーク型表示を作成("Benchmark.Optimization.All.ArrayClassInt32"));
        Assert.Equal("int",スケーラブルベンチマーク名フォーマッター.ベンチマーク型表示を作成("Benchmark.Optimization.All.IEnumerableInt32"));
        Assert.Equal("int",スケーラブルベンチマーク名フォーマッター.ベンチマーク型表示を作成("Benchmark.Optimization.All.Range_start_count"));
        Assert.Equal("int",スケーラブルベンチマーク名フォーマッター.ベンチマーク型表示を作成("Benchmark.Optimization.All.PersistedInt32"));
        Assert.Equal("ClassInt32",スケーラブルベンチマーク名フォーマッター.ベンチマーク型表示を作成("Benchmark.Optimization.All.TsavoriteClassInt32"));
        Assert.Equal(string.Empty,スケーラブルベンチマーク名フォーマッター.ベンチマーク型表示を作成("System.Array.FLinq"));
    }

    private static ベンチマークソースタブ定義 比較ソースタブを作成() =>
        new(
            ベンチマークソースタブ.最適化書き換え,
            "Test.Comparison",
            "Test",
            "Left",
            "Left",
            "Left.Assembly",
            "Right",
            "Right",
            "Right.Assembly");

    private static ベンチマークケース ケースを作成(string プロジェクト,string アセンブリ,string 型,string メソッド,string ベンチマークパス = "") =>
        new(プロジェクト,アセンブリ,型,メソッド,ベンチマークパス);

    private static ベンチマークケース ケースを作成(string プロジェクト,string アセンブリ,string 型,string メソッド,string 引数キー,IReadOnlyList<ベンチマーク引数> 引数一覧) =>
        new(プロジェクト,アセンブリ,型,メソッド,引数キー,引数一覧);

    private static 報告行 報告行を作成(string プロジェクト,string メソッド,string 型,double 平均) =>
        new(
            プロジェクト,
            メソッド,
            型,
            メソッド,
            "Completed",
            平均.ToString(global::System.Globalization.CultureInfo.InvariantCulture),
            string.Empty,
            string.Empty,
            "0",
            string.Empty,
            string.Empty,
            平均,
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
            0);

}
