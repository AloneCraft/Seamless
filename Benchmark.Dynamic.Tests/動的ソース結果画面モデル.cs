using Benchmark.Dynamic.Core;
using System.Collections.ObjectModel;

namespace Benchmark.Dynamic.Tests;

public sealed class 動的ソース結果画面モデル {
    [Fact,Trait("Category","Unit")]
    public void 行一覧() {
        var 共有=new ObservableCollection<global::Benchmark.Dynamic.UI.動的結果行>();
        var 画面モデル=new global::Benchmark.Dynamic.UI.動的ベンチマーク画面モデル(new 偽実行調停(),new メモリ設定());
        var 対象=new global::Benchmark.Dynamic.UI.動的ソース結果画面モデル(SourceTarget.SystemArray,共有,画面モデル);
        共有.Add(new global::Benchmark.Dynamic.UI.動的結果行 {
            実行ID="run",
            QueryID="query-array",
            対象=SourceTarget.SystemArray,
            QueryIndex=0,
            短縮式="Count",
            N=1,
            深度=1,
        });
        共有.Add(new global::Benchmark.Dynamic.UI.動的結果行 {
            実行ID="run",
            QueryID="query-list",
            対象=SourceTarget.SystemList,
            QueryIndex=0,
            短縮式="Count",
            N=1,
            深度=1,
        });
        var 行=Assert.Single(対象.行一覧);
        Assert.Equal("query-array",行.QueryID);
        共有.Clear();
        Assert.Empty(対象.行一覧);
    }

    [Fact,Trait("Category","Unit")]
    public void 進行割合と完了かは行数とQuery数から計算される() {
        var 画面モデル=new global::Benchmark.Dynamic.UI.動的ベンチマーク画面モデル(new 偽実行調停(),new メモリ設定());
        画面モデル.Query数=2;
        var 対象=new global::Benchmark.Dynamic.UI.動的ソース結果画面モデル(SourceTarget.SystemArray,画面モデル.結果一覧,画面モデル);
        Assert.Equal(0,対象.進行割合);
        Assert.False(対象.完了か);
        Assert.False(対象.進行ありか);

        画面モデル.結果一覧.Add(結果行を作成(SourceTarget.SystemArray,0));
        Assert.Equal(0.5,対象.進行割合);
        Assert.False(対象.完了か);

        画面モデル.結果一覧.Add(結果行を作成(SourceTarget.SystemArray,1));
        Assert.Equal(1,対象.進行割合);
        Assert.True(対象.完了か);
        Assert.True(対象.完了アイコン表示中か);
        Assert.False(対象.進行リング表示中か);
    }

    [Fact,Trait("Category","Unit")]
    public async Task 実行中はソースタブの進行状態が有効になる() {
        var 完了ゲート=new TaskCompletionSource();
        var 画面モデル=new global::Benchmark.Dynamic.UI.動的ベンチマーク画面モデル(
            new 偽実行調停{実行完了待機=完了ゲート.Task},
            new メモリ設定());
        var 対象=new global::Benchmark.Dynamic.UI.動的ソース結果画面モデル(SourceTarget.SystemArray,画面モデル.結果一覧,画面モデル);
        Assert.False(対象.進行ありか);

        var 実行タスク=画面モデル.実行コマンド.ExecuteAsync(null);
        Assert.True(画面モデル.実行中か);
        Assert.True(対象.進行ありか);
        Assert.True(対象.実行スピナー表示中か);
        Assert.False(対象.完了アイコン表示中か);

        完了ゲート.SetResult();
        await 実行タスク;
        Assert.False(画面モデル.実行中か);
    }

    private static global::Benchmark.Dynamic.UI.動的結果行 結果行を作成(SourceTarget 対象,int Query番号) {
        return new global::Benchmark.Dynamic.UI.動的結果行 {
            実行ID="run",
            QueryID="run/"+対象+"/"+Query番号,
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
        public Task? 実行完了待機{get;init;}

        public async Task<global::Benchmark.Dynamic.UI.動的履歴項目> 実行Async(
            global::Benchmark.Dynamic.UI.動的実行設定 設定,
            Action<global::Benchmark.Dynamic.UI.動的結果行> 結果受信時,
            Action<global::Benchmark.Dynamic.UI.動的実行進捗> 進捗受信時) {
            if(this.実行完了待機 is null) {
                throw new NotSupportedException();
            }
            await this.実行完了待機;
            return new global::Benchmark.Dynamic.UI.動的履歴項目 {
                実行ID="run-delayed",
                開始日時=DateTimeOffset.UnixEpoch,
                状態="Completed",
                条件="delayed",
            };
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
            return [];
        }

        public global::Benchmark.Dynamic.UI.動的実行条件Snapshot 実行条件を取得(string 実行ID) {
            throw new NotSupportedException();
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
