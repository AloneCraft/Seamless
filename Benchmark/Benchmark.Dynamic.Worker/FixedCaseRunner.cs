using Benchmark.Dynamic.Core;
using Benchmark.Infrastructure;
using System.Text.Json;

namespace Benchmark.Dynamic.Worker;

public sealed class FixedCaseRunner(BenchmarkRepository repository){
    public IReadOnlyList<Measurement> Run(int count=256,int seed=42,int flags=15,string? caseID=null,CancellationToken cancellationToken=default){
        if(count<0||count>65536){throw new ArgumentOutOfRangeException(nameof(count));}
        using var lease=MeasurementLease.Acquire(cancellationToken);
        var environment=EnvironmentSnapshot.Capture("Balanced-v1");var id=Guid.NewGuid().ToString("N");
        var run=new Run(id,"Dynamic.Fixed","Running",JsonSerializer.Serialize(new{count,seed,flags,caseID}),JsonSerializer.Serialize(environment),BuildFingerprint.Capture(typeof(FixedCaseRunner).Assembly.Location),DateTimeOffset.UtcNow);
        repository.SaveRun(run);var recorded=new List<Measurement>();
        try{
            foreach(var item in FixedCaseCatalog.All.Where(item=>caseID is null||item.ID==caseID)){
                SystemResources.Check(true);cancellationToken.ThrowIfCancellationRequested();
                var source=FixedCaseCatalog.Emit(item,flags);
                var compiled=new DynamicQueryCompiler().Compile(new("fixed-"+Guid.NewGuid().ToString("N"),"Benchmark.Dynamic.Generated.FixedQuery",source,item.ID+".cs"),cancellationToken);
                if(!compiled.Success){throw new InvalidOperationException(item.ID+": "+string.Join("\n",compiled.Diagnostics.Where(d=>d.Severity=="Error").Select(d=>d.Message)));}
                var loaded=compiled.LoadedQuery!;
                try{
                    using var fixture=new SourceFixtureFactory().Create(SourceTarget.SystemArray,count,Seed256.Parse(seed.ToString()));
                    var expected=loaded.Run(DynamicImplementationKind.System,fixture.Primary,fixture.Secondary);
                    foreach(var implementation in new[]{DynamicImplementationKind.FLinqUnoptimized,DynamicImplementationKind.FLinq,DynamicImplementationKind.Ideal}){
                        var actual=loaded.Run(implementation,fixture.Primary,fixture.Secondary);
                        var comparison=DynamicQueryComparer.Compare(expected,actual);
                        if(!comparison.IsMatch){throw new InvalidDataException(item.ID+" "+implementation+": "+JsonSerializer.Serialize(comparison));}
                    }
                    var measured=new MeasurementEngine().Measure(loaded,fixture.Primary,fixture.Secondary,0,0,cancellationToken);
                    var input=Fingerprint.OfJSON(new{fixture.PrimaryValues,fixture.SecondaryValues,count,seed,Generator="fixed-v1"});
                    foreach(var summary in measured.Measurements){
                        var conditions=new Conditions(item.ID,input,count,"System.Int32","System","Array",summary.Implementation.ToString(),flags.ToString(),"{}","{}",environment.Key,"Balanced-v1",SemanticsVersion:Fingerprint.Of(item.Expression+item.IdealBody));
                        var samples=summary.Samples.Select(sample=>new Sample(sample.ElapsedNanoseconds,sample.OperationCount,sample.AllocatedBytes,Gen0:sample.Gen0Collections,Gen1:sample.Gen1Collections,Gen2:sample.Gen2Collections)).ToArray();
                        var result=Measurement.Create(Guid.NewGuid().ToString("N"),id,conditions,Fingerprint.OfJSON(expected),samples) with{DiagnosticsJSON=JsonSerializer.Serialize(new{item.ID,item.Methods,item.Expression,item.IdealBody,compiled.ExpandedSource})};
                        repository.SaveMeasurement(result);recorded.Add(result);
                    }
                }finally{if(!loaded.UnloadAndVerify().Unloaded){throw new InvalidOperationException("Fixed case assembly did not unload.");}}
            }
            if(recorded.Count==0){throw new ArgumentException("Unknown fixed case.");}
            repository.SaveRun(run with{Status="Passed",CompletedAt=DateTimeOffset.UtcNow});return recorded;
        }catch{repository.SaveRun(run with{Status="Failed",CompletedAt=DateTimeOffset.UtcNow});throw;}
    }
}
