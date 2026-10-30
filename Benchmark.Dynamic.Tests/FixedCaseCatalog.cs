using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Worker;

namespace Benchmark.Dynamic.Tests;

[Collection(DynamicWorkerTestCollection.Name)]
public sealed class FixedCaseCatalog{
    [Fact,Trait("Category","FixedReference")]
    public void PreservesBoundaryResultsAndExceptions(){
        int[][] inputs=[[],[0],[int.MaxValue],[int.MinValue],[int.MaxValue,int.MaxValue],[int.MaxValue,int.MaxValue,int.MaxValue],[int.MinValue,int.MinValue],[-1,-1,0,15],[int.MaxValue,-1,int.MaxValue],[2147483632,16,-2147483632],[0,int.MaxValue,int.MaxValue],[int.MaxValue,int.MaxValue,0],[0,16,1,17,-16,-15],Enumerable.Range(-32,65).ToArray()];
        var failures=new List<string>();
        var additional=new[]{
            new Core.FixedCase("conditional-group-sum",["GroupBy","Sum"],"source.GroupBy(x=>x&15).Select(g=>g.Key==0?g.Sum():0).Sum(x=>(long)x)",null,"Long","Scalar",string.Empty),
            new Core.FixedCase("first-group-sum",["GroupBy","Sum","Take"],"source.GroupBy(x=>x&15).Select(g=>g.Sum()).Take(1).Sum(x=>(long)x)",null,"Long","Scalar",string.Empty),
            new Core.FixedCase("key-before-group-overflow",["GroupBy","Sum"],"source.GroupBy(x=>10/x).Select(g=>g.Sum()).Sum(x=>(long)x)",null,"Long","Scalar",string.Empty),
            new Core.FixedCase("short-circuit-group-sum",["GroupBy","Sum"],"source.GroupBy(x=>x&15).Select(g=>g.Key!=0||g.Sum()==0).LongCount(x=>x)",null,"Long","Scalar",string.Empty),
            new Core.FixedCase("captured-conditional-group-sum",["GroupBy","Sum"],"source.GroupBy(x=>x&15).Select(g=>g.Key==0?g.Sum(x=>unchecked(x+second.Length)):0).Sum(x=>(long)x)",null,"Long","Scalar",string.Empty),
            new Core.FixedCase("long-group-sum",["Select","GroupBy","Sum"],"source.Select(x=>(long)x*int.MaxValue).GroupBy(x=>x&15).Select(g=>g.Sum()).Sum()",null,"Long","Scalar",string.Empty),
            new Core.FixedCase("decimal-group-sum",["Select","GroupBy","Sum"],"source.Select(x=>x>0?decimal.MaxValue:-decimal.MaxValue).GroupBy(x=>0).Select(g=>g.Sum()).LongCount(x=>x==0)",null,"Long","Scalar",string.Empty)
        };
        foreach(var item in Core.FixedCaseCatalog.All.Concat(additional)){
            var compilation=new DynamicQueryCompiler().Compile(new("boundary-"+Guid.NewGuid().ToString("N"),"Benchmark.Dynamic.Generated.FixedQuery",Core.FixedCaseCatalog.Emit(item),item.ID+"-boundaries.cs"));
            if(!compilation.Success){File.WriteAllText(Path.Combine(Path.GetTempPath(),item.ID+".compilation.cs"),compilation.ExpandedSource+"\n/*\n"+Core.FixedCaseCatalog.Emit(item)+"\n*/");}
            Assert.True(compilation.Success,item.ID+": "+string.Join("\n",compilation.Diagnostics.Where(d=>d.Severity=="Error").Select(d=>d.Message)));
            SaveEvidence(item,compilation,"boundaries");
            var loaded=compilation.LoadedQuery!;
            var failuresBefore=failures.Count;
            try{
                foreach(var values in inputs){
                    var expected=Capture(loaded,DynamicImplementationKind.System,values);
                    foreach(var implementation in new[]{DynamicImplementationKind.Ideal,DynamicImplementationKind.FLinqUnoptimized,DynamicImplementationKind.FLinq}){
                        if(!loaded.Has(implementation)){continue;}
                        var actual=Capture(loaded,implementation,values);
                        var context=item.ID+" "+implementation+" ["+string.Join(",",values)+"]";
                        if(expected.Exception!=actual.Exception){
                            failures.Add(context+": expected "+expected.Exception+", actual "+actual.Exception);
                        }else if(expected.Value is not null&&(actual.Value is null||!DynamicQueryComparer.Compare(expected.Value,actual.Value).IsMatch)){
                            failures.Add(context+": result mismatch");
                        }
                    }
                }
            }finally{
                if(failures.Count!=failuresBefore){File.WriteAllText(Path.Combine(Path.GetTempPath(),item.ID+".expanded.cs"),compilation.ExpandedSource);}
                Assert.True(loaded.UnloadAndVerify().Unloaded);
            }
        }
        Assert.True(failures.Count==0,string.Join("\n",failures));
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static (DynamicQueryValue? Value,Type? Exception) Capture(LoadedDynamicQuery query,DynamicImplementationKind implementation,int[] values){
        try{return(query.Run(implementation,values,new[]{-1,0,1,16,int.MaxValue}),null);}
        catch(Exception exception){return(null,exception.GetType());}
    }

    [Fact,Trait("Category","FixedReference")]
    public void PreservesFixedReferenceEvaluationCounts(){
        var records=new List<object>();
        foreach(var item in Core.FixedCaseCatalog.All){
            var source=EvaluationProbeRewriter.Instrument(Core.FixedCaseCatalog.Emit(item));
            var compilation=new DynamicQueryCompiler().Compile(new("counted-fixed-"+Guid.NewGuid().ToString("N"),"Benchmark.Dynamic.Generated.FixedQuery",source,item.ID+"-counted.cs"));
            Assert.True(compilation.Success,item.ID+": "+string.Join("\n",compilation.Diagnostics.Where(d=>d.Severity=="Error").Select(d=>d.Message)));
            SaveEvidence(item,compilation,"counted",source);
            var loaded=compilation.LoadedQuery!;
            try{
                foreach(var count in new[]{0,1,16}){
                    using var fixture=new SourceFixtureFactory().Create(SourceTarget.SystemArray,count,Seed256.Parse("42"));
                    var counts=new Dictionary<DynamicImplementationKind,long[]>();
                    foreach(var implementation in new[]{DynamicImplementationKind.System,DynamicImplementationKind.FLinqUnoptimized,DynamicImplementationKind.FLinq}){
                        Assert.True(loaded.Has(implementation));
                        EvaluationProbe.Reset();
                        loaded.Run(implementation,fixture.Primary,fixture.Secondary);
                        counts[implementation]=EvaluationProbe.CountsByLambda;
                    }
                    records.Add(new{item.ID,count,counts});
                    var problem=EvaluationCountComparer.CompareByLambda(counts);
                    Assert.True(problem is null,item.ID+" count="+count+": "+problem);
                }
            }finally{
                File.WriteAllText(Path.Combine(Path.GetTempPath(),"fixed-reference-evaluation.json"),System.Text.Json.JsonSerializer.Serialize(records));
                Assert.True(loaded.UnloadAndVerify().Unloaded);
            }
        }
    }

    private static void SaveEvidence(Core.FixedCase item,DynamicCompilationResult compilation,string phase,string? source=null){
        var directory=Directory.CreateDirectory(Path.Combine(Path.GetTempPath(),"FixedReferenceEvidence",phase)).FullName;
        source??=Core.FixedCaseCatalog.Emit(item);
        File.WriteAllText(Path.Combine(directory,item.ID+".source.cs"),source);
        File.WriteAllText(Path.Combine(directory,item.ID+".generated.cs"),compilation.ExpandedSource);
        File.WriteAllText(Path.Combine(directory,item.ID+".json"),System.Text.Json.JsonSerializer.Serialize(new{
            item.ID,item.OriginalSHA256,OriginalIdealBody=item.IdealBody,EffectiveIdealBody=Core.FixedCaseCatalog.EffectiveIdealBody(item),
            SourceSHA256=Benchmark.Infrastructure.Fingerprint.Of(source),
            GeneratedSHA256=Benchmark.Infrastructure.Fingerprint.Of(compilation.ExpandedSource),compilation.Diagnostics,compilation.OptimizationTracePayloads
        }));
    }

    [Fact]
    public void PreservesAllHandwrittenLoops(){
        Assert.Equal(25,Core.FixedCaseCatalog.All.Count);
        Assert.Equal(25,Core.FixedCaseCatalog.All.Select(item=>item.ID).Distinct().Count());
        foreach(var item in Core.FixedCaseCatalog.All){
            var compilation=new DynamicQueryCompiler().Compile(new("ideal-"+Guid.NewGuid().ToString("N"),"Benchmark.Dynamic.Generated.FixedQuery",Core.FixedCaseCatalog.Emit(item),item.ID+".cs"));
            Assert.True(compilation.Success,item.ID+": "+string.Join("\n",compilation.Diagnostics.Where(d=>d.Severity=="Error").Select(d=>d.Message)));
            var loaded=compilation.LoadedQuery!;
            try{
                foreach(var count in new[]{0,1,16}){
                    using var fixture=new SourceFixtureFactory().Create(SourceTarget.SystemArray,count,Seed256.Parse("42"));
                    var expected=loaded.Run(DynamicImplementationKind.System,fixture.Primary,fixture.Secondary);
                    var ideal=loaded.Run(DynamicImplementationKind.Ideal,fixture.Primary,fixture.Secondary);
                    Assert.True(DynamicQueryComparer.Compare(expected,ideal).IsMatch,item.ID+" count="+count);
                }
            }finally{Assert.True(loaded.UnloadAndVerify().Unloaded);}
        }
    }
}
