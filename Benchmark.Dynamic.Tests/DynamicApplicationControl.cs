using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.UI;
using System.Text.Json;

namespace Benchmark.Dynamic.Tests;

public sealed class 動的外部検証集計{
    [Fact]
    public void 検証結果を取得(){
        {
            var 集計=new UI.動的外部検証集計(1);
            Assert.Equal("Unavailable",集計.検証結果を取得("Completed"));
            集計.結果を追加(WorkerExecutionStatus.Succeeded,true,true);
            Assert.Equal("Passed",集計.検証結果を取得("Completed"));
        }
        {
            var 集計=new UI.動的外部検証集計(2);
            集計.結果を追加(WorkerExecutionStatus.Succeeded,true,true);
            Assert.Equal("Inconclusive",集計.検証結果を取得("Completed"));
            集計.結果を追加(WorkerExecutionStatus.OptimizationNonConverged,true,false);
            Assert.Equal("Failed",集計.検証結果を取得("Completed"));
            Assert.Equal("Unavailable",集計.検証結果を取得("Canceled"));
        }
        foreach(var 状態 in new[]{WorkerExecutionStatus.CompilationFailed,WorkerExecutionStatus.SemanticMismatch,WorkerExecutionStatus.ExecutionFailed,WorkerExecutionStatus.ExpansionIncomplete,WorkerExecutionStatus.AssemblyUnloadFailed}){
            var 集計=new UI.動的外部検証集計(1);集計.結果を追加(状態,false,false);
            Assert.Equal("Failed",集計.検証結果を取得("Completed"));
        }
    }

    [Fact]
    public void 外部設定は必須キーと型を検証する(){
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<動的外部実行設定>("{}",動的アプリ操作サービス.設定JSON));
        var 設定=new 動的外部実行設定{Query数=1,N=1,入力要素数=4,最大深度=1,Seed="42",演算子集合モード=OperatorSetMode.Common,対象一覧=[SourceTarget.SystemArray],要素型=QueryElementType.Int,最適化フラグ=15,計測設定=new(),ストレージ設定=new()};
        var 実行設定=設定.実行設定を作る();
        Assert.Equal(1,実行設定.Query数);
        Assert.Throws<ArgumentOutOfRangeException>(()=>(設定 with{Query数=0}).実行設定を作る());
        Assert.Throws<ArgumentException>(()=>(設定 with{Seed="bad"}).実行設定を作る());
        var JSON=JsonSerializer.SerializeToNode(設定,動的アプリ操作サービス.設定JSON)!;
        JSON["eval"]="anything";
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<動的外部実行設定>(JSON.ToJsonString(),動的アプリ操作サービス.設定JSON));
    }
}
