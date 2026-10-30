using Benchmark.Dynamic.Core;
using Benchmark.Infrastructure;
using System.Text.Json;

namespace Benchmark.Dynamic.Worker;

public sealed class FixedCaseRunner(BenchmarkRepository repository){
    private static readonly DynamicImplementationKind[] ValidationImplementations=[DynamicImplementationKind.System,DynamicImplementationKind.FLinqUnoptimized,DynamicImplementationKind.FLinq,DynamicImplementationKind.Ideal];
    public IReadOnlyList<Measurement> Run(int count=256,int seed=42,int flags=15,string? caseID=null,CancellationToken cancellationToken=default){
        if(count<0||count>65536){throw new ArgumentOutOfRangeException(nameof(count));}
        using var lease=MeasurementLease.Acquire(cancellationToken);
        var environment=EnvironmentSnapshot.Capture("Balanced-v1");var id=Guid.NewGuid().ToString("N");
        var run=new Run(id,"Dynamic.Fixed","Running",JsonSerializer.Serialize(new{count,seed,flags,caseID}),JsonSerializer.Serialize(environment),BuildFingerprint.Capture(typeof(FixedCaseRunner).Assembly.Location),DateTimeOffset.UtcNow);
        repository.SaveRun(run);var recorded=new List<Measurement>();
        var validations=new List<object>();
        var matched=FixedCaseCatalog.All.Where(item=>caseID is null||item.ID==caseID).ToArray();
        try{
            if(matched.Length==0){throw new ArgumentException("Unknown fixed case.");}
            foreach(var item in matched){
                SystemResources.Check(true);cancellationToken.ThrowIfCancellationRequested();
                var source=FixedCaseCatalog.Emit(item,flags);
                var compiled=new DynamicQueryCompiler().Compile(new("fixed-"+Guid.NewGuid().ToString("N"),"Benchmark.Dynamic.Generated.FixedQuery",source,item.ID+".cs"),cancellationToken);
                if(!compiled.Success){throw new InvalidOperationException(item.ID+": "+string.Join("\n",compiled.Diagnostics.Where(d=>d.Severity=="Error").Select(d=>d.Message)));}
                var loaded=compiled.LoadedQuery!;
                try{
                    using var fixture=new SourceFixtureFactory().Create(SourceTarget.SystemArray,count,Seed256.Parse(seed.ToString()));
                    foreach(var implementation in ValidationImplementations){
                        if(!loaded.Has(implementation)){throw new InvalidDataException(item.ID+": missing "+implementation);}
                    }
                    var validation=DynamicQueryValidator.Validate(loaded,fixture.Primary,fixture.Secondary,ValidationImplementations,true,cancellationToken);
                    string? skipReason=null;
                    if(!validation.ShouldMeasure){skipReason=validation.Status.ToString();}
                    if(validation.Status==WorkerExecutionStatus.Succeeded&&validation.Exceptions.Count!=0){skipReason="MatchingExceptions";}
                    validations.Add(new{item.ID,item.OriginalSHA256,Status=validation.Status.ToString(),validation.Exceptions,validation.Comparisons,validation.Fallbacks,MeasurementSkipped=!validation.ShouldMeasure,SkipReason=skipReason,SourceSHA256=Fingerprint.Of(source),GeneratedSHA256=Fingerprint.Of(compiled.ExpandedSource)});
                    if(validation.Status!=WorkerExecutionStatus.Succeeded){throw new InvalidDataException(item.ID+": "+JsonSerializer.Serialize(validation));}
                    if(!validation.ShouldMeasure){continue;}
                    var expected=validation.Results[DynamicImplementationKind.System];
                    var measured=new MeasurementEngine().Measure(loaded,fixture.Primary,fixture.Secondary,0,0,cancellationToken);
                    var input=Fingerprint.OfJSON(new{fixture.PrimaryValues,fixture.SecondaryValues,count,seed,Generator="fixed-v1"});
                    foreach(var summary in measured.Measurements){
                        var conditions=new Conditions(item.ID,input,count,"System.Int32","System","Array",summary.Implementation.ToString(),flags.ToString(),"{}","{}",environment.Key,"Balanced-v1",SemanticsVersion:Fingerprint.Of(item.Expression+FixedCaseCatalog.EffectiveIdealBody(item)));
                        var samples=summary.Samples.Select(sample=>new Sample(sample.ElapsedNanoseconds,sample.OperationCount,sample.AllocatedBytes,Gen0:sample.Gen0Collections,Gen1:sample.Gen1Collections,Gen2:sample.Gen2Collections)).ToArray();
                        var result=Measurement.Create(Guid.NewGuid().ToString("N"),id,conditions,Fingerprint.OfJSON(expected),samples) with{DiagnosticsJSON=JsonSerializer.Serialize(new{item.ID,item.Methods,item.Expression,item.OriginalSHA256,OriginalIdealBody=item.IdealBody,IdealBody=FixedCaseCatalog.EffectiveIdealBody(item),SourceSHA256=Fingerprint.Of(source),GeneratedSHA256=Fingerprint.Of(compiled.ExpandedSource),compiled.ExpandedSource})};
                        repository.SaveMeasurement(result);recorded.Add(result);
                    }
                }finally{if(!loaded.UnloadAndVerify().Unloaded){throw new InvalidOperationException("Fixed case assembly did not unload.");}}
            }
            SaveStatus("Passed");return recorded;
        }catch{SaveStatus("Failed");throw;}

        void SaveStatus(string status){
            repository.SaveRun(run with{Status=status,SettingsJSON=JsonSerializer.Serialize(new{count,seed,flags,caseID,MatchedCaseCount=matched.Length,Validation=validations}),CompletedAt=DateTimeOffset.UtcNow});
        }
    }
}
