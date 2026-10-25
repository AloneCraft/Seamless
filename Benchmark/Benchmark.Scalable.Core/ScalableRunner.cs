using Benchmark.Infrastructure;
using Seamless.Execution;
using System.Diagnostics;
using System.Text.Json;

namespace Benchmark.Scalable.Core;

/// <summary>全件配列を作らず、生成・操作・検証をストリーミングで行う。</summary>
public sealed class ScalableRunner(BenchmarkRepository repository){
    public Measurement Run(ScalableRequest request,Action<ScalableProgress>? progress=null,CancellationToken cancellationToken=default){
        request.Validate();SystemResources.Check(true);
        SystemResources.CheckBudget(request.MemoryLimitBytes);
        using var lease=MeasurementLease.Acquire(cancellationToken);
        var id=Guid.NewGuid().ToString("N");
        var environment=EnvironmentSnapshot.Capture("scalable-content-v2");
        var code=BuildFingerprint.Capture(typeof(ScalableRunner).Assembly.Location);
        var run=new Run(id,"Scalable","Running",JsonSerializer.Serialize(request),JsonSerializer.Serialize(environment),code,DateTimeOffset.UtcNow);
        repository.SaveRun(run);
        var samples=new List<Sample>();
        ProfileSettings? effective=null;ProfileResolution? resolution=null;string growth="{}";string? digest=null;double transitions=0;
        try{
            // 初回はJITと資源初期化のウォームアップ。性能判定には含めない。
            for(var sampleIndex=-1;sampleIndex<request.Samples;sampleIndex++){
                SystemResources.Check(true);cancellationToken.ThrowIfCancellationRequested();
                var parentBudget=new ResourceBudget(request.MemoryLimitBytes,request.DiskLimitBytes);
                var store=new ScalableStore(request,environment.ProfileEnvironment,parentBudget:parentBudget);
                try{
                    effective=store.Effective;
                    resolution=store.Owner.ResolvedProfiles["data"];
                    growth=JsonSerializer.Serialize(store.Owner.GetGrowth("data").Descriptor);
                    var allocated=GC.GetTotalAllocatedBytes(true);
                    var collections=new[]{GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)};
                    using var content=new ContentDigest();
                    using var process=Process.GetCurrentProcess();
                    var observedWorkingSet=process.WorkingSet64;
                    var timer=Stopwatch.StartNew();long transitionDisk=0;
                    for(var index=0;index<request.Count;index++){
                        if(index%request.ChunkSize==0){
                            SystemResources.Check();cancellationToken.ThrowIfCancellationRequested();
                            process.Refresh();observedWorkingSet=Math.Max(observedWorkingSet,process.WorkingSet64);
                            progress?.Invoke(new(id,Math.Max(0,sampleIndex),request.Samples,index,request.Count,"追加"));
                        }
                        store.Data.Add(index,Value(index,request.Seed));
                        if(request.Adaptive&&index+1==request.Count/2&&sampleIndex>=0){
                            transitions+=this.Reconfigure(ref store,request,environment.ProfileEnvironment,id,sampleIndex,cancellationToken,out var diskDuringChange);
                            transitionDisk=Math.Max(transitionDisk,diskDuringChange);
                        }
                    }
                    var result=Execute(store.Data,request,cancellationToken,store.Owner,out var operationDisk,content);
                    effective=store.Current;
                    timer.Stop();
                    var measuredAllocated=GC.GetTotalAllocatedBytes(true)-allocated;
                    var measuredCollections=new[]{GC.CollectionCount(0)-collections[0],GC.CollectionCount(1)-collections[1],GC.CollectionCount(2)-collections[2]};
                    process.Refresh();observedWorkingSet=Math.Max(observedWorkingSet,process.WorkingSet64);
                    var verification=Stopwatch.StartNew();VerifyContent(content,request,store.Data.CanRemove,cancellationToken);verification.Stop();
                    if(digest is not null&&digest!=result){throw new InvalidDataException("Repeated samples produced different data.");}
                    digest=result;
                    if(sampleIndex>=0){
                        var disk=Math.Max(transitionDisk,checked(store.Owner.Budget.PeakTemporaryStorageBytes+operationDisk));
                        var sample=new Sample(timer.Elapsed.TotalNanoseconds,Math.Max(1,request.Count),measuredAllocated,
                            process.PeakWorkingSet64,disk,measuredCollections[0],measuredCollections[1],measuredCollections[2],observedWorkingSet,parentBudget.PeakMemoryBytes,parentBudget.PeakTemporaryStorageBytes,verification.Elapsed.TotalNanoseconds);
                        samples.Add(sample);
                    }
                }finally{
                    try{if(sampleIndex>=0){transitions+=this.RecordGrowth(store,id,sampleIndex);}}
                    finally{store.Dispose();}
                }
            }
            var parameters=new ProfileParameters(request.ExplicitSettings,effective!,resolution!.ProfileID??resolution.Applied.ToString(),resolution.Reason,resolution.ProfileVersion,resolution.EvidenceRunIDs,request.Profile.ToString(),resolution.Settings,ProfileEvidence.Current);
            var input=Fingerprint.OfJSON(new{request.Count,request.Seed,Generator="xor-sequence-v1"});
            var conditions=new Conditions(request.WorkloadID,input,request.Count,"System.Int32|System.Int32",request.Backend,request.Kind.ToString(),request.ActualImplementation.ToString(),"Native",JsonSerializer.Serialize(parameters),growth,environment.Key,"scalable-content-v2",Adaptive:request.Adaptive,IO:ProfileIO.Describe(IOConfiguration.Parse(request.IOMode,request.AccessPattern)));
            var measurement=Measurement.Create(Guid.NewGuid().ToString("N"),id,conditions,digest!,samples.ToArray()) with{TransitionNanoseconds=transitions};
            repository.SaveMeasurement(measurement);
            repository.SaveRun(run with{Status="Passed",CompletedAt=DateTimeOffset.UtcNow});
            progress?.Invoke(new(id,request.Samples,request.Samples,request.Count,request.Count,"完了"));
            return measurement;
        }catch(Exception exception){
            var status="Failed";if(exception is OperationCanceledException){status="Canceled";}
            repository.SaveRun(run with{Status=status,CompletedAt=DateTimeOffset.UtcNow,SettingsJSON=JsonSerializer.Serialize(new{Request=request,Error=exception.ToString(),ValidSamples=samples.Count})});
            throw;
        }
    }
    private double Reconfigure(ref ScalableStore current,ScalableRequest request,string environment,string run,int interval,CancellationToken cancellationToken,out long diskBytes){
        diskBytes=0;
        var before=current.Current;
        var after=before;
        if(request.Backend=="Tsavorite"){
            var target=before.LogMemoryBytes!.Value*2;
            after=before with{LogMemoryBytes=target};
        }else{
            after=before with{InitialCapacity=before.InitialCapacity!.Value*2};
        }
        var timer=Stopwatch.StartNew();var source=Digest(current.Data);var applied=false;var reason="Measured boundary rebuild";var targetDigest="";ScalableStore? replacement=null;double growthElapsed=0;
        try{
            SystemResources.Check(true);
            replacement=new(request,environment,after,current.Owner.Budget.Parent);
            var count=0;
            foreach(var pair in current.Data){
                if(++count%request.ChunkSize==0){SystemResources.Check();cancellationToken.ThrowIfCancellationRequested();}
                replacement.Data.Add(pair);
            }
            targetDigest=Digest(replacement.Data);
            diskBytes=checked(current.Owner.Budget.PeakTemporaryStorageBytes+replacement.Owner.Budget.PeakTemporaryStorageBytes);
            if(source!=targetDigest){throw new InvalidDataException("Reconfiguration lost or duplicated data.");}
            var previous=current;current=replacement;replacement=null;applied=true;
            try{growthElapsed=this.RecordGrowth(previous,run,interval);}finally{previous.Dispose();}
        }catch(OperationCanceledException){throw;}
        catch(Exception exception){reason=exception.GetType().Name+": "+exception.Message;}
        finally{
            replacement?.Dispose();timer.Stop();
            repository.SaveChange(new(Guid.NewGuid().ToString("N"),run,interval,JsonSerializer.Serialize(before),JsonSerializer.Serialize(after),reason,applied,timer.Elapsed.TotalNanoseconds,source,targetDigest,DateTimeOffset.UtcNow));
        }
        return timer.Elapsed.TotalNanoseconds+growthElapsed;
    }
    private double RecordGrowth(ScalableStore store,string run,int interval){
        double elapsed=0;
        foreach(var change in store.Owner.GrowthEvents){
            var before=JsonSerializer.Serialize(new{change.Component,change.Context.Resource,Bytes=change.Context.CurrentBytes});
            var after=JsonSerializer.Serialize(new{change.Component,change.Context.Resource,Bytes=change.Decision.TargetBytes});
            repository.SaveChange(new(Guid.NewGuid().ToString("N"),run,interval,before,after,change.Decision.Reason,true,change.ElapsedNanoseconds,"","",change.RecordedAt));
            elapsed+=change.ElapsedNanoseconds??0;
        }
        return elapsed;
    }
    private static string Execute(ScalableData data,ScalableRequest request,CancellationToken cancellationToken,global::Seamless.Execution.Store owner,out long diskBytes,ContentDigest content){
        if(request.ActualImplementation!=ScalableImplementation.Native){return ScalableLinq.Execute(data,request,owner,cancellationToken,out diskBytes,content);}
        diskBytes=0;
        long expected=0,actual=0,count=0;
        long expectedCount=request.Count;
        switch(request.Workload){
            case Workload.Collections:
                for(var index=0;index<request.Count;index++){
                    Check(index,request.ChunkSize,cancellationToken);
                    if(!data.TryGetValue(index,out var value)||value!=Value(index,request.Seed)){throw new InvalidDataException("Lookup mismatch.");}
                    data[index]=value^0x55AA;
                    if(index%3==0&&data.CanRemove){if(!data.Remove(index)){throw new InvalidDataException("Delete failed.");}}
                    else{expected+=((long)index<<32)+(uint)(Value(index,request.Seed)^0x55AA);count++;}
                }
                if(data.Count!=count){throw new InvalidDataException("Count mismatch.");}
                // 削除を含む処理では、残った件数を期待値として列挙した件数と照合する。
                expectedCount=count;count=0;
                foreach(var pair in data){
                    count++;
                    if(pair.Key<0||pair.Key>=request.Count||(data.CanRemove&&pair.Key%3==0)||pair.Value!=(Value(pair.Key,request.Seed)^0x55AA)){throw new InvalidDataException("Enumeration mismatch.");}
                    actual+=((long)pair.Key<<32)+(uint)pair.Value;content.Add(((long)pair.Key<<32)+(uint)pair.Value);
                }
                break;
            case Workload.FilterAggregate:
                foreach(var pair in data){Check(count++,request.ChunkSize,cancellationToken);if(pair.Key%2==0){actual+=pair.Value;content.Add(pair.Value);}}
                for(var index=0;index<request.Count;index+=2){expected+=Value(index,request.Seed);}
                break;
            case Workload.Join:
                foreach(var pair in data){
                    Check(count++,request.ChunkSize,cancellationToken);
                    var key=(int)((pair.Key+1L)%request.Count);
                    if(!data.TryGetValue(key,out var other)){throw new InvalidDataException("Join lookup mismatch.");}
                    actual+=(long)pair.Value*other;content.Add((long)pair.Value*other);
                }
                for(var index=0;index<request.Count;index++){expected+=(long)Value(index,request.Seed)*Value((int)((index+1L)%request.Count),request.Seed);}
                break;
            case Workload.Sort:
                var sortRequest=request with{MemoryLimitBytes=Math.Max(0,request.MemoryLimitBytes-owner.Budget.MemoryBytes),DiskLimitBytes=Math.Max(0,request.DiskLimitBytes-owner.Budget.TemporaryStorageBytes),TemporaryDirectory=Path.GetDirectoryName(owner.DirectoryPath)??request.TemporaryDirectory};
                actual=ExternalSort.Verify(data.Values,sortRequest,cancellationToken,out diskBytes,content,owner.Budget.Parent??owner.Budget);
                for(var index=0;index<request.Count;index++){expected+=Value(index,request.Seed);}
                count=data.Count;
                break;
            default:throw new ArgumentOutOfRangeException(nameof(request.Workload));
        }
        if(actual!=expected||count!=expectedCount){
            var seen=new bool[256];long nativeCount=0;
            foreach(var pair in data){nativeCount++;if(pair.Key>=0&&pair.Key<seen.Length){seen[pair.Key]=true;}}
            var missing=Enumerable.Range(0,(int)Math.Min(request.Count,seen.Length)).Where(index=>!seen[index]).ToArray();
            var lookupMissing=missing.Where(index=>!data.TryGetValue(index,out _)).ToArray();
            throw new InvalidDataException($"Result mismatch. Expected={expected} Actual={actual}, DeclaredCount={data.Count}, NativeCount={nativeCount}, MissingPrefix={string.Join(',',missing)}, LookupMissing={string.Join(',',lookupMissing)}");
        }
        return content.Fingerprint();
    }
    private static void VerifyContent(ContentDigest actual,ScalableRequest request,bool canRemove,CancellationToken cancellationToken){
        using var expected=new ContentDigest();
        for(var index=0;index<request.Count;index++){
            Check(index,request.ChunkSize,cancellationToken);
            switch(request.Workload){
                case Workload.Collections:
                    if(!canRemove||index%3!=0){expected.Add(((long)index<<32)+(uint)(Value(index,request.Seed)^0x55AA));}
                    break;
                case Workload.FilterAggregate:
                    if(index%2==0){expected.Add(Value(index,request.Seed));}
                    break;
                case Workload.Join:
                    expected.Add((long)Value(index,request.Seed)*Value((int)((index+1L)%request.Count),request.Seed));
                    break;
                case Workload.Sort:expected.Add(Value(index,request.Seed));break;
            }
        }
        actual.AssertMatches(expected);
    }
    private static int Value(int index,int seed)=>index^seed;
    private static void Check(long index,int chunk,CancellationToken cancellationToken){if(index%chunk==0){SystemResources.Check();cancellationToken.ThrowIfCancellationRequested();}}
    private static string Digest(ScalableData data){
        using var content=new ContentDigest();foreach(var pair in data){content.Add(((long)pair.Key<<32)+(uint)pair.Value);}
        return content.Fingerprint();
    }
}
