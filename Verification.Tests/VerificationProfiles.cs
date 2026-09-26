using Benchmark.Dynamic.Core;
using System.Text.Json;
using Verification.Core;
using Verification.Infrastructure;
namespace Verification.Tests;

public sealed class VerificationProfiles{
    [Fact]public void Dynamic(){
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..",".."));
        var plan=Infrastructure.VerificationProfiles.Dynamic(root,"quick","1",2,SourceTarget.SystemArray);
        Assert.Equal(2,plan.Cases.Length);
        Assert.All(plan.Cases,item=>{
            Assert.Equal("dynamic-worker",item.Adapter);
            Assert.True(item.IsPerformance);
            Assert.Contains("random-query-v6",item.Conditions.CaseVersion,StringComparison.Ordinal);
            Assert.NotEqual("repository-fixtures",item.Conditions.InputHash);
        });
        Assert.Throws<ArgumentOutOfRangeException>(()=>Infrastructure.VerificationProfiles.Dynamic(root,"quick",operators:2,depth:3));
    }
    [Fact]public void RepresentativeBenchmark_UsesRawPairedSamples(){
        var conditions=Infrastructure.VerificationProfiles.EnvironmentConditions();
        var item=Infrastructure.VerificationProfiles.RepresentativeBenchmarks(conditions,"quick").Single();
        Assert.Equal("headless-benchmark",item.Adapter);
        Assert.True(item.IsPerformance);
        Assert.Contains("samples9",item.Conditions.MeasurementVersion,StringComparison.Ordinal);
        var json=JsonSerializer.Serialize(new{
            FLinqNanoseconds=12d,
            FLinqAllocation=4d,
            BaselineNanoseconds=10d,
            BaselineAllocation=3d,
            FLinqSamples=new[]{10d,11d,12d,13d,14d,15d,16d,17d,18d},
            FLinqAllocationSamples=new long[]{1,2,3,4,5,6,7,8,9},
            BaselineSamples=new[]{8d,9d,10d,11d,12d,13d,14d,15d,16d},
            BaselineAllocationSamples=new long[]{0,1,2,3,4,5,6,7,8},
        });
        var result=HeadlessBenchmarkVerificationAdapter.Parse(item,json,1234);
        Assert.Equal(CaseStatus.Passed,result.Status);
        Assert.Equal(9,result.ElapsedNanoseconds.Length);
        Assert.Equal(18,result.Measurements!.Length);
        Assert.Equal(5,result.AllocatedBytes);
        Assert.Equal(1234,result.PeakPrivateBytes);
    }
    [Fact]public void Sweep_ProvidesResumableCasesAndCapacityMetrics(){
        var conditions=Infrastructure.VerificationProfiles.EnvironmentConditions();
        var cases=Infrastructure.VerificationProfiles.SweepCases(conditions,"quick",representativeOnly:false);
        Assert.Equal(6,cases.Length);
        Assert.All(cases,item=>{
            Assert.Equal("parameter-sweep",item.Adapter);
            Assert.Equal("capacity-sweep-v1",item.Conditions.MeasurementVersion);
        });
        var json=JsonSerializer.Serialize(new{
            失敗種別=0,
            最大成功Count=128L,
            エントリCount=128L,
            追加経過ミリ秒=2.5d,
            検索平均ナノ秒=(double?)42d,
            失敗メッセージ=string.Empty,
            索引バケット数=64L,
            溢れバケット数=20L,
            メモリログバイト数=8192L,
            ディスクログバイト数=(long?)null,
        });
        var result=ParameterSweepVerificationAdapter.Parse(cases[0],json,2048);
        Assert.Equal(CaseStatus.Passed,result.Status);
        Assert.Equal(128,result.Metrics!["maximum-count"]);
        Assert.Equal(64,result.Metrics["index-buckets"]);
        Assert.Equal(20,result.Metrics["overflow-buckets"]);
        Assert.Equal(8192,result.Metrics["memory-log-bytes"]);
        Assert.False(result.Metrics.ContainsKey("disk-log-bytes"));
        var legacy=System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
        foreach(var property in new[]{"索引バケット数","溢れバケット数","メモリログバイト数","ディスクログバイト数"}){
            legacy.Remove(property);
        }
        Assert.Equal(CaseStatus.Passed,ParameterSweepVerificationAdapter.Parse(cases[0],legacy.ToJsonString(),2048).Status);
        Assert.Equal(2,result.Measurements!.Length);
        Assert.Equal(2048,result.PeakPrivateBytes);
        foreach(var failure in new[]{0,1,2,3,4,5,6,7,8,-1}){
            var payload=System.Text.Json.Nodes.JsonNode.Parse(json)!;
            payload["失敗種別"]=failure;
            var classified=ParameterSweepVerificationAdapter.Parse(cases[0],payload.ToJsonString(),2048);
            if(failure is 0 or 1 or 2 or 3 or 6 or 7){
                Assert.Equal(CaseStatus.Passed,classified.Status);
            }else{
                Assert.Equal(CaseStatus.Failed,classified.Status);
            }
        }
    }
    [Fact]public void Scalable_EnumeratesQuickAndFullCasesAndKeepsRawSamples(){
        var conditions=Infrastructure.VerificationProfiles.EnvironmentConditions();
        var quick=Infrastructure.VerificationProfiles.ScalableCases(conditions,"quick",representativeOnly:false);
        var full=Infrastructure.VerificationProfiles.ScalableCases(conditions,"full",representativeOnly:false);
        Assert.Equal(2,quick.Length);
        Assert.Equal(720,full.Length);
        Assert.Equal(full.Length,full.Select(static item=>item.Id).Distinct(StringComparer.Ordinal).Count());
        var json=JsonSerializer.Serialize(new{
            状態="Completed",
            ログ=string.Empty,
            平均ナノ秒=(double?)40,
            割り当てバイト数=(long?)70,
            結果ダイジェスト="digest",
            サンプル一覧=new[]{10d,20d,30d,40d,50d,60d,70d},
            GC収集回数一覧=new[]{1,2,3},
            ProcessPeakPrivateBytes=4096L,
        });
        var result=ScalableVerificationAdapter.Parse(quick[0],json,quick[0].Arguments[0],1024);
        Assert.Equal(CaseStatus.Passed,result.Status);
        Assert.Equal(7,result.ElapsedNanoseconds.Length);
        Assert.Equal(7,result.Measurements!.Length);
        Assert.Equal(70,result.AllocatedBytes);
        Assert.Equal("digest",result.ResultDigest);
        Assert.Equal(4096,result.PeakPrivateBytes);
    }
}
