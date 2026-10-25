using Benchmark.Infrastructure;
using Benchmark.Tsavorite.ParameterSweep;
using System.Text.Json;

namespace Benchmark.Scalable.Core;

public sealed class CapacitySweepRunner(BenchmarkRepository repository){
    public async Task<IReadOnlyList<パラメータスイープ結果>> RunAsync(パラメータスイープ実行オプション options,bool smoke=true,IProgress<パラメータスイープ結果>? progress=null,CancellationToken cancellationToken=default){
        using var lease=await Task.Run(()=>MeasurementLease.Acquire(cancellationToken),cancellationToken).ConfigureAwait(false);SystemResources.Check(true);
        var environment=EnvironmentSnapshot.Capture("capacity-sweep-v1");var id=Guid.NewGuid().ToString("N");
        var run=new Run(id,"Scalable.Sweep","Running",JsonSerializer.Serialize(new{Options=options,Smoke=smoke}),JsonSerializer.Serialize(environment),BuildFingerprint.Capture(typeof(パラメータスイープ実行器).Assembly.Location),DateTimeOffset.UtcNow);
        repository.SaveRun(run);
        try{
            IReadOnlyList<パラメータスイープケース> cases;
            if(smoke){cases=パラメータスイープ計画.スモークケース一覧を作成(options);}
            else{cases=パラメータスイープ計画.既定ケース一覧を作成(options);}
            var results=new List<パラメータスイープ結果>();
            foreach(var item in cases){
                SystemResources.Check(true);cancellationToken.ThrowIfCancellationRequested();
                SystemResources.CheckBudget(checked(item.設定.Indexサイズ+item.設定.Memoryサイズ+2*item.設定.Pageサイズ));
                var result=await new パラメータスイープ実行器().ケースを実行Async(item,options,cancellationToken).ConfigureAwait(false);
                var conditions=new Conditions(result.ケース名,Fingerprint.OfJSON(new{result.コレクション,result.ジェネリック名,Generator="sweep-v1"}),options.最大成功Count,result.ジェネリック名,"Tsavorite",result.バックエンド.ToString(),"Capacity","Native",JsonSerializer.Serialize(item.設定),"{\"ID\":\"fixed\",\"Version\":1,\"ParametersJSON\":\"{}\"}",environment.Key,"capacity-sweep-v1");
                var measurement=Measurement.Create(Guid.NewGuid().ToString("N"),id,conditions,"capacity-contract-v1",[]) with{
                    DiagnosticsJSON=JsonSerializer.Serialize(result),Capacity=new(result.最大成功Count,result.失敗種別.ToString())
                };
                if(cancellationToken.IsCancellationRequested){throw new OperationCanceledException(cancellationToken);}
                repository.SaveMeasurement(measurement);results.Add(result);progress?.Report(result);
            }
            repository.SaveRun(run with{Status="Passed",CompletedAt=DateTimeOffset.UtcNow});return results;
        }catch(Exception exception){
            var status="Failed";if(exception is OperationCanceledException){status="Canceled";}
            repository.SaveRun(run with{Status=status,CompletedAt=DateTimeOffset.UtcNow});throw;
        }
    }
}
