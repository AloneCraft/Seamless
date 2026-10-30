using Benchmark.Infrastructure;
using Seamless.Execution;
using Seamless.Linq;

namespace Benchmark.Scalable.Core;

internal static class ScalableLinq{
    internal static string Execute(ScalableData data,ScalableRequest request,global::Seamless.Execution.Store owner,CancellationToken cancellationToken,out long diskBytes,ContentDigest content){
        var memory=Math.Max(0,request.MemoryLimitBytes-owner.Budget.MemoryBytes);
        using var scope=new ExecutionScope(new(){MemoryLimitBytes=request.MemoryLimitBytes,TemporaryStorageLimitBytes=request.DiskLimitBytes,ParentBudget=owner.Budget.Parent??owner.Budget,TemporaryDirectory=request.TemporaryDirectory??Path.GetDirectoryName(owner.DirectoryPath)??Path.Combine(Path.GetTempPath(),"Seamless","Execution"),CancellationToken=cancellationToken});
        using var systemWork=(owner.Budget.Parent??owner.Budget).ReserveMemory(0);
        IEnumerable<long> values;
        var scoped=request.ActualImplementation==ScalableImplementation.Scoped;
        long expected=0,count=0,actual=0,expectedRead=request.Count;
        switch(request.Workload){
            case Workload.FilterAggregate:
                expectedRead=(request.Count+1)/2;
                if(scoped){values=scope.Query(data).Where(pair=>pair.Key%2==0).Select(pair=>(long)pair.Value);}
                else{values=data.Where(pair=>pair.Key%2==0).Select(pair=>(long)pair.Value);}
                for(long index=0;index<request.Count;index+=2){expected+=(int)index^request.Seed;}
                count=data.Count;
                break;
            case Workload.Join:
                if(scoped){values=scope.Query(data).Join(data,pair=>(int)((pair.Key+1L)%request.Count),pair=>pair.Key,(left,right)=>(long)left.Value*right.Value);}
                else{
                    if(request.Count*128L>memory){throw new InvalidOperationException("System.Linq.Joinの推定作業領域が上限を超えます。Scopedを選んでください。");}
                    systemWork.Resize(request.Count*128L);
                    values=data.Join(data,pair=>(int)((pair.Key+1L)%request.Count),pair=>pair.Key,(left,right)=>(long)left.Value*right.Value);
                }
                for(long index=0;index<request.Count;index++){expected+=((int)index^request.Seed)*(long)((int)((index+1)%request.Count)^request.Seed);}
                count=data.Count;
                break;
            case Workload.Sort:
                if(scoped){values=scope.Query(data).Select(pair=>(long)pair.Value).OrderBy(value=>value);}
                else{
                    if(request.Count*64L>memory){throw new InvalidOperationException("System.Linq.OrderByの推定作業領域が上限を超えます。Scopedを選んでください。");}
                    systemWork.Resize(request.Count*64L);
                    values=data.Select(pair=>(long)pair.Value).OrderBy(value=>value);
                }
                for(long index=0;index<request.Count;index++){expected+=(int)index^request.Seed;}
                break;
            default:throw new InvalidOperationException("Collections uses the native collection API.");
        }
        var previous=long.MinValue;long read=0;
        foreach(var value in values){
            if(read++%request.ChunkSize==0){SystemResources.Check();cancellationToken.ThrowIfCancellationRequested();}
            if(request.Workload==Workload.Sort){if(value<previous){throw new InvalidDataException("Ordering mismatch.");}previous=value;count++;}
            actual+=value;content.Add(value);
        }
        if(count!=request.Count||read!=expectedRead||actual!=expected){
            long referenceCount=0,referenceSum=0;
            foreach(var pair in data){referenceCount++;referenceSum+=pair.Value;}
            throw new InvalidDataException($"LINQ result or count mismatch. Count={count}/{request.Count}, Sum={actual}/{expected}, NativeCount={referenceCount}, NativeSum={referenceSum}, ScopeDisk={scope.Budget.PeakTemporaryStorageBytes}.");
        }
        diskBytes=scope.Budget.PeakTemporaryStorageBytes;
        return content.Fingerprint();
    }
}
