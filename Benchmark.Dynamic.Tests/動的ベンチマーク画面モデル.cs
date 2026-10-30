using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.UI;

namespace Benchmark.Dynamic.Tests;

public sealed class 動的ベンチマーク画面モデル {
    [Fact,Trait("Category","Unit")]
    public void Constructor() {
        var 設定=new メモリ設定();
        var 対象=new global::Benchmark.Dynamic.UI.動的ベンチマーク画面モデル(new 偽実行調停(),設定);
        Assert.Equal(10,対象.Query数);
        Assert.Equal(8,対象.N);
        Assert.Equal(1024,対象.入力要素数);
        Assert.Equal(8,対象.最大深度);
        Assert.Equal(OperatorSetMode.Common,対象.演算子集合モード);
        var 選択済み=Assert.Single(対象.ソース選択一覧,項目=>項目.選択済みか);
        Assert.Equal(SourceTarget.SystemArray,選択済み.対象);
        Assert.Equal(8,対象.ソース画面一覧.Count);
    }

    [Fact,Trait("Category","Unit")]
    public void Constructor_履歴とごみ箱を読み戻す() {
        var 調停=new 偽実行調停 {
            履歴=[new 動的履歴項目 {
                実行ID="run-history",
                開始日時=DateTimeOffset.UnixEpoch,
                状態="Completed",
                条件="history",
            }],
            ごみ箱=[new 動的履歴項目 {
                実行ID="run-trash",
                開始日時=DateTimeOffset.UnixEpoch,
                状態="Canceled",
                条件="trash",
                削除済みか=true,
            }],
        };
        var 対象=new global::Benchmark.Dynamic.UI.動的ベンチマーク画面モデル(調停,new メモリ設定());
        Assert.Equal("run-history",Assert.Single(対象.履歴一覧).実行ID);
        Assert.Equal("run-trash",Assert.Single(対象.ごみ箱一覧).実行ID);
    }

    [Fact,Trait("Category","Unit")]
    public void Constructor_範囲外または破損したPreferencesを安全な初期値へ正規化する() {
        var 設定=new メモリ設定 {
            ["Benchmark.Dynamic.Query数"]=-1,
            ["Benchmark.Dynamic.N"]=100,
            ["Benchmark.Dynamic.入力要素数"]=200_000,
            ["Benchmark.Dynamic.最大深度"]=-8,
            ["Benchmark.Dynamic.Seed"]="invalid",
            ["Benchmark.Dynamic.選択ソース"]="UnknownSource",
        };

        var 対象=new global::Benchmark.Dynamic.UI.動的ベンチマーク画面モデル(new 偽実行調停(),設定);

        Assert.Equal(1,対象.Query数);
        Assert.Equal(64,対象.N);
        Assert.Equal(100_000,対象.入力要素数);
        Assert.Equal(1,対象.最大深度);
        Assert.True(Seed256.TryParse(対象.Seed文字列,out _));
        Assert.Equal(SourceTarget.SystemArray,Assert.Single(対象.ソース選択一覧,static 項目=>項目.選択済みか).対象);
    }

    [Fact,Trait("Category","Unit")]
    public void N() {
        var 対象=new global::Benchmark.Dynamic.UI.動的ベンチマーク画面モデル(new 偽実行調停(),new メモリ設定());
        対象.N=0;
        Assert.Equal(1,対象.N);
        対象.Query数=0;
        Assert.Equal(1,対象.Query数);
        対象.Query数=1001;
        Assert.Equal(1000,対象.Query数);
        対象.N=64;
        Assert.Equal(32,対象.最大深度上限);
        対象.最大深度=40;
        Assert.Equal(32,対象.最大深度);
        対象.N=3;
        Assert.Equal(3,対象.最大深度);
        対象.入力要素数=100001;
        Assert.Equal(100000,対象.入力要素数);
    }

    [Fact,Trait("Category","Unit")]
    public void ソース選択一覧() {
        var 設定=new メモリ設定 {
            ["Benchmark.Dynamic.選択ソース"]="SystemList,SeamlessTsavorite",
        };
        var 対象=new global::Benchmark.Dynamic.UI.動的ベンチマーク画面モデル(new 偽実行調停(),設定);
        Assert.Equal(
            [SourceTarget.SystemList,SourceTarget.SeamlessTsavorite],
            対象.ソース選択一覧.Where(項目=>項目.選択済みか).Select(項目=>項目.対象));
    }

    [Fact,Trait("Category","Unit")]
    public void 設定復元Seed再生成コマンド() {
        var 調停=new 偽実行調停 {
            実行条件=new global::Benchmark.Dynamic.UI.動的実行条件Snapshot(
                1,
                23,
                12,
                4_096,
                10,
                "0x1",
                OperatorSetMode.Maximum,
                [SourceTarget.SystemList,SourceTarget.SeamlessPersisted],
                new WorkerMeasurementProfile(),
                RandomQueryGenerator.GeneratorVersion,
                QueryCapabilityProfile.CurrentProfileVersion,
                BuiltInOperatorCatalog.OperatorSetVersion,
                "capability"),
        };
        var 対象=new global::Benchmark.Dynamic.UI.動的ベンチマーク画面モデル(調停,new メモリ設定());
        var 履歴=new 動的履歴項目 {
            実行ID="run-settings",
            開始日時=DateTimeOffset.UnixEpoch,
            状態="Completed",
            条件="settings",
        };
        対象.設定復元Seed再生成コマンド.Execute(履歴);
        Assert.Equal(23,対象.Query数);
        Assert.Equal(12,対象.N);
        Assert.Equal(4_096,対象.入力要素数);
        Assert.Equal(10,対象.最大深度);
        Assert.Equal(OperatorSetMode.Maximum,対象.演算子集合モード);
        Assert.Equal(
            [SourceTarget.SystemList,SourceTarget.SeamlessPersisted],
            対象.ソース選択一覧.Where(static 項目=>項目.選択済みか).Select(static 項目=>項目.対象));
        Assert.True(Seed256.TryParse(対象.Seed文字列,out _));
        Assert.NotEqual("0x1",対象.Seed文字列);
    }

    [Fact,Trait("Category","Unit")]
    public void 全選択を切り替える_引数なしは全タブを対象にする() {
        var 対象=new global::Benchmark.Dynamic.UI.動的ベンチマーク画面モデル(new 偽実行調停(),new メモリ設定());
        var Array行=結果行を作成(SourceTarget.SystemArray,0);
        var List行=結果行を作成(SourceTarget.SystemList,0);
        対象.結果一覧.Add(Array行);
        対象.結果一覧.Add(List行);
        Assert.False(対象.全選択済みか);

        対象.全選択を切り替える();
        Assert.True(Array行.選択済みか);
        Assert.True(List行.選択済みか);
        Assert.True(対象.全選択済みか);

        対象.全選択を切り替える();
        Assert.False(Array行.選択済みか);
        Assert.False(List行.選択済みか);
        Assert.False(対象.全選択済みか);
    }

    [Fact,Trait("Category","Unit")]
    public void 全選択を切り替える_対象指定は該当タブのみに影響する() {
        var 対象=new global::Benchmark.Dynamic.UI.動的ベンチマーク画面モデル(new 偽実行調停(),new メモリ設定());
        var Array行=結果行を作成(SourceTarget.SystemArray,0);
        var List行=結果行を作成(SourceTarget.SystemList,0);
        対象.結果一覧.Add(Array行);
        対象.結果一覧.Add(List行);

        対象.全選択を切り替える(SourceTarget.SystemArray);
        Assert.True(Array行.選択済みか);
        Assert.False(List行.選択済みか);
        Assert.True(対象.タブ選択状態を取得(SourceTarget.SystemArray));
        Assert.False(対象.タブ選択状態を取得(SourceTarget.SystemList));
        Assert.False(対象.全選択済みか);
    }

    [Fact,Trait("Category","Unit")]
    public void 行の選択済みかを直接変更すると全選択済みかへ反映される() {
        var 対象=new global::Benchmark.Dynamic.UI.動的ベンチマーク画面モデル(new 偽実行調停(),new メモリ設定());
        var Array行=結果行を作成(SourceTarget.SystemArray,0);
        var List行=結果行を作成(SourceTarget.SystemList,0);
        対象.結果一覧.Add(Array行);
        対象.結果一覧.Add(List行);

        Array行.選択済みか=true;
        Assert.False(対象.全選択済みか);

        List行.選択済みか=true;
        Assert.True(対象.全選択済みか);
    }

    private static global::Benchmark.Dynamic.UI.動的結果行 結果行を作成(SourceTarget 対象,int Query番号) {
        return new global::Benchmark.Dynamic.UI.動的結果行 {
            実行ID="run-1",
            QueryID="run-1/"+対象+"/"+Query番号,
            対象=対象,
            QueryIndex=Query番号,
            短縮式="source",
            N=8,
            深度=4,
        };
    }

    private sealed class メモリ設定:Dictionary<string,object>,global::Benchmark.Dynamic.UI.I動的UI設定 {
        public T 取得<T>(string キー,T 既定値) {
            return this.TryGetValue(キー,out var 値)&&値 is T 型付き?型付き:既定値;
        }

        public void 保存<T>(string キー,T 値) {
            this[キー]=値!;
        }
    }

    private sealed class 偽実行調停:global::Benchmark.Dynamic.UI.I動的実行調停サービス {
        public IReadOnlyList<global::Benchmark.Dynamic.UI.動的履歴項目> 履歴{get;init;}=[];
        public IReadOnlyList<global::Benchmark.Dynamic.UI.動的履歴項目> ごみ箱{get;init;}=[];
        public global::Benchmark.Dynamic.UI.動的実行条件Snapshot? 実行条件{get;init;}

        public Task<global::Benchmark.Dynamic.UI.動的履歴項目> 実行Async(
            global::Benchmark.Dynamic.UI.動的実行設定 設定,
            Action<global::Benchmark.Dynamic.UI.動的結果行> 結果受信時,
            Action<global::Benchmark.Dynamic.UI.動的実行進捗> 進捗受信時) {
            throw new NotSupportedException();
        }

        public Task<global::Benchmark.Dynamic.UI.動的履歴項目> Runを再実行Async(
            string 親実行ID,
            Action<global::Benchmark.Dynamic.UI.動的結果行> 結果受信時,
            Action<global::Benchmark.Dynamic.UI.動的実行進捗> 進捗受信時) {
            throw new NotSupportedException();
        }

        public Task<global::Benchmark.Dynamic.UI.動的履歴項目> Queryを再実行Async(
            string 親QueryID,
            Action<global::Benchmark.Dynamic.UI.動的結果行> 結果受信時,
            Action<global::Benchmark.Dynamic.UI.動的実行進捗> 進捗受信時) {
            throw new NotSupportedException();
        }

        public Task<global::Benchmark.Dynamic.UI.動的履歴項目> 複数Queryを再実行Async(
            IReadOnlyList<string> 親QueryID一覧,
            Action<global::Benchmark.Dynamic.UI.動的結果行> 結果受信時,
            Action<global::Benchmark.Dynamic.UI.動的実行進捗> 進捗受信時) {
            throw new NotSupportedException();
        }

        public Task<global::Benchmark.Dynamic.UI.動的履歴項目> Queryを縮小Async(
            string 親QueryID,
            Action<global::Benchmark.Dynamic.UI.動的結果行> 結果受信時,
            Action<global::Benchmark.Dynamic.UI.動的実行進捗> 進捗受信時) {
            throw new NotSupportedException();
        }

        public Task キャンセルAsync() {
            return Task.CompletedTask;
        }

        public IReadOnlyList<global::Benchmark.Dynamic.UI.動的履歴項目> 履歴を取得(bool ごみ箱か) {
            return ごみ箱か?this.ごみ箱:this.履歴;
        }

        public global::Benchmark.Dynamic.UI.動的実行条件Snapshot 実行条件を取得(string 実行ID) {
            return this.実行条件??throw new NotSupportedException();
        }

        public Task ごみ箱へ移動Async(string 実行ID) {
            return Task.CompletedTask;
        }

        public Task 復元Async(string 実行ID) {
            return Task.CompletedTask;
        }

        public Task 完全削除Async(string 実行ID) {
            return Task.CompletedTask;
        }
    }
}
