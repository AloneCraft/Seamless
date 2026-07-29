using Benchmark.Dynamic.Core;
using System.Collections.ObjectModel;

namespace Benchmark.Dynamic.Tests;

public sealed class 動的ソース結果画面モデル {
    [Fact,Trait("Category","Unit")]
    public void 行一覧() {
        var 共有=new ObservableCollection<global::Benchmark.Dynamic.UI.動的結果行>();
        var 対象=new global::Benchmark.Dynamic.UI.動的ソース結果画面モデル(SourceTarget.SystemArray,共有);
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
}
