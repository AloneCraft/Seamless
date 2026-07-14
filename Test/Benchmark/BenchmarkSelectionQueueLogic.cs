namespace Test.Benchmark;

[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class ベンチマーク選択キューロジック {
    [Fact]
    public void 選択ケースを作成() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var カタログ = カタログを作成(ソースタブ);
        {
            var 選択一覧 = 行を選択(カタログ,ソースタブ,true);
            var ケース一覧 = global::UI.ベンチマーク選択キューロジック.選択ケースを作成(カタログ,ソースタブ,選択一覧).ToArray();
            Assert.Equal(4,ケース一覧.Length);
            Assert.Contains(ケース一覧,項目 => 項目.プロジェクト==ソースタブ.左プロジェクト);
            Assert.Contains(ケース一覧,項目 => 項目.プロジェクト==ソースタブ.右プロジェクト);
        }
        {
            var 選択一覧 = new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase) {
                [global::UI.ベンチマーク選択キューロジック.行選択キーを作成(ソースタブ,"不明-行")]=true,
            };
            foreach(var 行 in global::UI.ベンチマーク行グループ化ロジック.行を作成(カタログ,ソースタブ)) {
                選択一覧[global::UI.ベンチマーク選択キューロジック.行選択キーを作成(ソースタブ,行.行キー)]=false;
            }
            Assert.Empty(global::UI.ベンチマーク選択キューロジック.選択ケースを作成(カタログ,ソースタブ,選択一覧));
        }
    }

    [Fact]
    public void 仕様確認01() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var カタログ = new[]{
            new global::UI.ベンチマークケース(ソースタブ.左プロジェクト,ソースタブ.左アセンブリ名,"Benchmark.System.Linq.Enumerable.FLinqInt32","Aggregate_func"),
            new global::UI.ベンチマークケース(ソースタブ.右プロジェクト,ソースタブ.右アセンブリ名,"Benchmark.System.Linq.Enumerable.ZLinqInt32","Aggregate_func"),
        };
        var 選択一覧 = 行を選択(カタログ,ソースタブ,true);

        var ケース一覧 = global::UI.ベンチマーク選択キューロジック.選択ケースを作成(カタログ,ソースタブ,選択一覧).ToArray();

        Assert.Equal(2,ケース一覧.Length);
        Assert.Contains(ケース一覧,項目 => 項目.プロジェクト==ソースタブ.左プロジェクト&&項目.アセンブリ名==ソースタブ.左アセンブリ名);
        Assert.Contains(ケース一覧,項目 => 項目.プロジェクト==ソースタブ.右プロジェクト&&項目.アセンブリ名==ソースタブ.右アセンブリ名);
    }

    [Fact]
    public void 全タブから選択ケースを作成() {
        {
            var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
            var カタログ = カタログを作成(ソースタブ);
            var 選択一覧 = 行を選択(カタログ,ソースタブ,true);
            var スナップショット = global::UI.ベンチマーク選択キューロジック.全タブから選択ケースを作成(カタログ,[ソースタブ],選択一覧);
            var キュー = global::UI.ベンチマーク選択キューロジック.全タブから選択ケースを作成(カタログ,[ソースタブ],選択一覧);
            Assert.Equal(4,スナップショット.Count);
            Assert.Equal(
                スナップショット.Select(ケース識別子を作成).ToArray(),
                キュー.Select(ケース識別子を作成).ToArray());
        }
        {
            var 永続化 = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.Seamless永続化);
            var Tsavorite = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.SeamlessTsavorite);
            Assert.Equal(永続化.右プロジェクト,Tsavorite.右プロジェクト);
            var カタログ = new[]{
                new global::UI.ベンチマークケース(永続化.右プロジェクト,永続化.右アセンブリ名,"Benchmark.Persisted.Linq.Enumerable.LinqInt32","Sum"),
                new global::UI.ベンチマークケース(Tsavorite.右プロジェクト,Tsavorite.右アセンブリ名,"Benchmark.Tsavorite.Linq.Enumerable.LinqInt32","Sum"),
            };
            var 選択一覧 = new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase);
            foreach(var タブ in new[] { 永続化,Tsavorite }) {
                foreach(var 行 in global::UI.ベンチマーク行グループ化ロジック.行を作成(カタログ,タブ)) {
                    選択一覧[global::UI.ベンチマーク選択キューロジック.行選択キーを作成(タブ,行.行キー)]=true;
                }
            }
            var キュー = global::UI.ベンチマーク選択キューロジック.全タブから選択ケースを作成(カタログ,[永続化,Tsavorite],選択一覧);
            Assert.Equal(2,キュー.Count);
            Assert.Contains(キュー,項目 => 項目.アセンブリ名==永続化.右アセンブリ名);
            Assert.Contains(キュー,項目 => 項目.アセンブリ名==Tsavorite.右アセンブリ名);
        }
        {
            var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
            var カタログ = カタログを作成(ソースタブ);
            var 選択一覧 = 行を選択(カタログ,ソースタブ,true);
            var 上書きケース = new global::UI.ベンチマークケース(ソースタブ.左プロジェクト,"Live.Assembly","Live.Type","Aggregate");
            var キュー = global::UI.ベンチマーク選択キューロジック.全タブから選択ケースを作成(
                カタログ,
                [ソースタブ],
                選択一覧,
                _ => new[] { 上書きケース });
            var 項目 = Assert.Single(キュー);
            Assert.Equal("Live.Type",項目.型完全名);
        }
    }

    private static string ケース識別子を作成(global::UI.ベンチマークケース ベンチマーク) =>
        string.Join("|",ベンチマーク.プロジェクト,ベンチマーク.アセンブリ名,ベンチマーク.型完全名,ベンチマーク.メソッド名,ベンチマーク.引数キー);

    private static global::UI.ベンチマークケース[] カタログを作成(global::UI.ベンチマークソースタブ定義 ソースタブ) => [
        new(ソースタブ.左プロジェクト,ソースタブ.左アセンブリ名,"Left.Type","Aggregate"),
        new(ソースタブ.左プロジェクト,ソースタブ.左アセンブリ名,"Left.Type","Sum"),
        new(ソースタブ.右プロジェクト,ソースタブ.右アセンブリ名,"Right.Type","Aggregate"),
        new(ソースタブ.右プロジェクト,ソースタブ.右アセンブリ名,"Right.Type","Sum"),
    ];

    private static Dictionary<string,bool> 行を選択(
        IReadOnlyList<global::UI.ベンチマークケース> カタログ,
        global::UI.ベンチマークソースタブ定義 ソースタブ,
        bool 選択済みか) {
        var 選択一覧 = new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase);
        foreach(var 行 in global::UI.ベンチマーク行グループ化ロジック.行を作成(カタログ,ソースタブ)) {
            選択一覧[global::UI.ベンチマーク選択キューロジック.行選択キーを作成(ソースタブ,行.行キー)]=選択済みか;
        }
        return 選択一覧;
    }
}
