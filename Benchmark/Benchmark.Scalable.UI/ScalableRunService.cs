using Benchmark.Infrastructure;
using Benchmark.Scalable.Core;

namespace Benchmark.Scalable.UI;

/// <summary>画面と外部操作が同じ Runner と元 Task の寿命を共有する。</summary>
public sealed class 大規模実行サービス{
    private readonly Func<ScalableRequest,Action<ScalableProgress>?,CancellationToken,Measurement> 実行;
    public アプリ操作排他 実行排他{get;}=new();
    public 大規模実行サービス(BenchmarkRepository 記録):this(new ScalableRunner(記録).Run){}
    public 大規模実行サービス(Func<ScalableRequest,Action<ScalableProgress>?,CancellationToken,Measurement> 実行)=>this.実行=実行;
    public Task<Measurement> 実行Async(ScalableRequest 要求,Action<ScalableProgress>? 進捗,CancellationToken 取消){
        要求.Validate();
        // 起動後の取消で Task を先に完了させない。Runner の finally と保存完了まで待つ。
        return Task.Run(()=>this.実行(要求,進捗,取消),CancellationToken.None);
    }
}
