using Benchmark.Infrastructure;
using Benchmark.Scalable.Core;
using Seamless.Execution;
using Xunit;

namespace Benchmark.Scalable.Tests;

public sealed class ScalableRunner{
    [Theory]
    [InlineData(0,Workload.Join)]
    [InlineData(1,Workload.Sort)]
    [InlineData(17,Workload.FilterAggregate)]
    [InlineData(4097,Workload.Join)]
    [InlineData(4097,Workload.Sort)]
    public void ContentAndBudgetMetrics(long count,Workload workload){
        var path=Path.Combine(Path.GetTempPath(),"scalable-content-"+Guid.NewGuid().ToString("N")+".db");
        try{
            var request=new ScalableRequest(Count:count,Workload:workload,Implementation:ScalableImplementation.Scoped,ChunkSize:16);
            var result=new Core.ScalableRunner(new(new(path))).Run(request);
            Assert.Equal("scalable-content-v2",result.Conditions.MeasurementVersion);
            Assert.All(result.Samples,sample=>{
                Assert.InRange(sample.ReservedPeakMemoryBytes!.Value,0,request.MemoryLimitBytes);
                Assert.InRange(sample.ReservedPeakTemporaryStorageBytes!.Value,0,request.DiskLimitBytes);
                Assert.True(sample.VerificationNanoseconds>=0);Assert.True(sample.ObservedIntervalWorkingSetBytes>0);
                Assert.Equal(sample.ProcessBytes,sample.ProcessLifetimePeakWorkingSetBytes);
            });
        }finally{File.Delete(path);}
    }
    [Fact]
    public void HistorySeparatesInitialAndActualCapacity(){
        var path=Path.Combine(Path.GetTempPath(),"scalable-settings-"+Guid.NewGuid().ToString("N")+".db");
        try{
            var repository=new BenchmarkRepository(new(path));
            var measurement=new Core.ScalableRunner(repository).Run(new(Count:1024,Kind:StoreKind.Temporary,Workload:Workload.FilterAggregate,ExplicitSettings:new(){IndexBytes=4096}));
            var parameters=System.Text.Json.JsonSerializer.Deserialize<ProfileParameters>(measurement.Conditions.ParametersJSON)!;
            Assert.Equal(4096,parameters.Initial!.IndexBytes);
            Assert.True(parameters.Effective.IndexBytes>parameters.Initial.IndexBytes);
            Assert.Equal("Passed",measurement.Status);
        }finally{File.Delete(path);}
    }
    [Theory]
    [InlineData("Tsavorite","Buffered","Sequential")]
    [InlineData("Tsavorite","Unbuffered","Random")]
    [InlineData("Persisted","Buffered","Random")]
    [InlineData("Persisted","Unbuffered","Sequential")]
    public void ExplicitIOIsAppliedAndRecorded(string backend,string mode,string pattern){
        var path=Path.Combine(Path.GetTempPath(),"scalable-io-"+Guid.NewGuid().ToString("N")+".db");
        try{
            var repository=new BenchmarkRepository(new(path));
            var result=new Core.ScalableRunner(repository).Run(new(Count:65,Backend:backend,Kind:StoreKind.Temporary,Workload:Workload.FilterAggregate,IOMode:mode,AccessPattern:pattern));
            Assert.Equal("Passed",result.Status);Assert.Contains(mode,result.Conditions.IO);Assert.Contains(pattern,result.Conditions.IO);
            Assert.Throws<ArgumentException>(()=>new ScalableRequest(Kind:StoreKind.Memory,IOMode:mode).Validate());
        }finally{File.Delete(path);}
    }
    [Fact]
    public async Task CapacityExplorationUsesSharedHistory(){
        var directory=Path.Combine(Path.GetTempPath(),"scalable-capacity-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try{
            var repository=new BenchmarkRepository(new(Path.Combine(directory,"history.db")));
            var results=await new CapacitySweepRunner(repository).RunAsync(new(){最大スイープ手順数=1,最大成功Count=8,検索試行Count=8,ケース別タイムアウト=TimeSpan.FromSeconds(5),作業ディレクトリ=directory});
            var run=Assert.Single(repository.GetRuns());Assert.Equal("Passed",run.Status);
            var measurements=repository.GetMeasurements(run.ID);Assert.Equal(results.Count,measurements.Count);
            Assert.All(measurements,measurement=>Assert.NotNull(measurement.Capacity));
        }finally{Directory.Delete(directory,true);}
    }
    [Fact]
    public void FailedReconfigurationRetainsOriginalData(){
        var path=Path.Combine(Path.GetTempPath(),"scalable-rebuild-failure-"+Guid.NewGuid().ToString("N")+".db");
        try{
            var repository=new BenchmarkRepository(new(path));
            var measurement=new Core.ScalableRunner(repository).Run(new(Count:32,Adaptive:true,MemoryLimitBytes:30000,ChunkSize:16));
            Assert.Equal("Passed",measurement.Status);
            var failed=repository.GetChanges(measurement.RunID).Where(change=>!change.Applied).ToArray();
            Assert.Equal(7,failed.Length);
            Assert.All(failed,change=>{Assert.NotEmpty(change.DigestBefore);Assert.NotEmpty(change.Reason);});
            Assert.Equal(7,measurement.Samples.Length);
        }finally{File.Delete(path);}
    }
    [Theory]
    [InlineData(ScalableImplementation.Scoped,Workload.FilterAggregate)]
    [InlineData(ScalableImplementation.Scoped,Workload.Sort)]
    [InlineData(ScalableImplementation.Scoped,Workload.Join)]
    [InlineData(ScalableImplementation.System,Workload.Join)]
    public void Linq(ScalableImplementation implementation,Workload workload){
        var path=Path.Combine(Path.GetTempPath(),"scalable-linq-"+Guid.NewGuid().ToString("N")+".db");
        try{
            var repository=new BenchmarkRepository(new(path));
            var measurement=new Core.ScalableRunner(repository).Run(new(Count:65,Workload:workload,Implementation:implementation,ChunkSize:16));
            Assert.Equal("Passed",measurement.Status);Assert.Equal(implementation.ToString(),measurement.Conditions.Implementation);
        }finally{File.Delete(path);}
    }
    [Theory]
    [InlineData("Tsavorite",StoreKind.Memory,Workload.Collections)]
    [InlineData("Persisted",StoreKind.Memory,Workload.Collections)]
    [InlineData("Tsavorite",StoreKind.Temporary,Workload.FilterAggregate)]
    [InlineData("Persisted",StoreKind.Temporary,Workload.Join)]
    [InlineData("Persisted",StoreKind.Temporary,Workload.Sort)]
    public void Run(string backend,StoreKind kind,Workload workload){
        var path=Path.Combine(Path.GetTempPath(),"scalable-test-"+Guid.NewGuid().ToString("N")+".db");
        try{
            var repository=new BenchmarkRepository(new(path));
            var measurement=new Core.ScalableRunner(repository).Run(new(Count:65,Backend:backend,Kind:kind,Workload:workload,ChunkSize:16));
            Assert.Equal("Passed",measurement.Status);Assert.Equal(7,measurement.Samples.Length);
            Assert.Equal("Passed",repository.GetRun(measurement.RunID)!.Status);
            Assert.Equal(measurement.Digest,repository.GetMeasurement(measurement.ID)!.Digest);
        }finally{File.Delete(path);}
    }
    [Fact]
    public void Reconfigure(){
        var path=Path.Combine(Path.GetTempPath(),"scalable-adaptive-"+Guid.NewGuid().ToString("N")+".db");
        try{
            var repository=new BenchmarkRepository(new(path));
            var measurement=new Core.ScalableRunner(repository).Run(new(Count:32,Adaptive:true,ChunkSize:16));
            var changes=repository.GetChanges(measurement.RunID).Where(change=>change.Reason=="Measured boundary rebuild").ToArray();
            Assert.Equal(7,changes.Length);
            Assert.All(changes,change=>{Assert.True(change.Applied,change.Reason);Assert.Equal(change.DigestBefore,change.DigestAfter);});
            Assert.True(measurement.TransitionNanoseconds>0);
        }finally{File.Delete(path);}
    }
}
