using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Worker;

namespace Benchmark.Dynamic.Tests;

[Collection(DynamicWorkerTestCollection.Name)]
public sealed class FixedCaseCatalog{
    [Fact,Trait("Category","FixedReference")]
    public void PreservesBoundaryResultsAndExceptions(){
        int[][] inputs=[[],[0],[3,4],[4,3],[int.MaxValue],[int.MinValue],[int.MaxValue,int.MaxValue],[int.MaxValue,int.MaxValue,int.MaxValue],[int.MinValue,int.MinValue],[-1,-1,0,15],[int.MaxValue,-1,int.MaxValue],[2147483632,16,-2147483632],[0,int.MaxValue,int.MaxValue],[int.MaxValue,int.MaxValue,0],[0,16,1,17,-16,-15],Enumerable.Range(-32,65).ToArray()];
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
        foreach(var item in Core.FixedCaseCatalog.All.Concat(additional).Concat(PredicateDemandCases())){
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
    private static (DynamicQueryValue? Value,Type? Exception) Capture(LoadedDynamicQuery query,DynamicImplementationKind implementation,int[] values,int[]? secondary=null){
        try{return(query.Run(implementation,values,secondary??new[]{-1,0,1,16,int.MaxValue}),null);}
        catch(Exception exception){return(null,exception.GetType());}
    }

    [Fact,Trait("Category","FixedReference")]
    public void SelectManyEvaluatesCollectionSelectorsOnlyAfterAnOuterElement(){
        var cases=new[]{
            new Core.FixedCase("selectmany-deferred-throw",["SelectMany","Sum"],"source.SelectMany(x=>new[]{10/second.Length}).Sum(x=>(long)x)",null,"Long","Scalar",string.Empty),
            new Core.FixedCase("selectmany-deferred-filter-throw",["SelectMany","Where","Select","Sum"],"source.SelectMany(x=>new[]{10/second.Length}).Where(x=>(x&1)==0).Select(x=>x*3+1).Sum(x=>(long)x)",null,"Long","Scalar",string.Empty),
            new Core.FixedCase("selectmany-deferred-range",["SelectMany","Range","Sum"],"source.SelectMany(x=>Enumerable.Range(0,second.Length-1)).Sum(x=>(long)x)",null,"Long","Scalar",string.Empty)
        };
        (int[] Primary,int[] Secondary)[] inputs=[([],[]),([1],[]),([],[1]),([1,2],[1]),([1,2],[1,2]),([0],[1,2,3])];
        var failures=new List<string>();
        foreach(var item in cases){
            var compilation=new DynamicQueryCompiler().Compile(new("deferred-selectmany-"+Guid.NewGuid().ToString("N"),"Benchmark.Dynamic.Generated.FixedQuery",Core.FixedCaseCatalog.Emit(item),item.ID+".cs"));
            Assert.True(compilation.Success,item.ID+": "+string.Join("\n",compilation.Diagnostics.Where(diagnostic=>diagnostic.Severity=="Error").Select(diagnostic=>diagnostic.Message)));
            SaveEvidence(item,compilation,"selectmany-empty-outer");
            var loaded=compilation.LoadedQuery!;
            try{
                foreach(var input in inputs){
                    for(var repeat=0;repeat<2;repeat++){
                        var expected=Capture(loaded,DynamicImplementationKind.System,input.Primary,input.Secondary);
                        foreach(var implementation in new[]{DynamicImplementationKind.FLinqUnoptimized,DynamicImplementationKind.FLinq}){
                            var actual=Capture(loaded,implementation,input.Primary,input.Secondary);
                            var context=item.ID+" "+implementation+" outer="+input.Primary.Length+" inner="+input.Secondary.Length+" repeat="+repeat;
                            if(expected.Exception!=actual.Exception){failures.Add(context+": expected "+expected.Exception+", actual "+actual.Exception);}
                            else if(expected.Value is not null&&(actual.Value is null||!DynamicQueryComparer.Compare(expected.Value,actual.Value).IsMatch)){failures.Add(context+": result mismatch");}
                        }
                    }
                }
            }finally{Assert.True(loaded.UnloadAndVerify().Unloaded);}
        }
        Assert.True(failures.Count==0,string.Join("\n",failures));
    }

    private static Core.FixedCase[] PredicateDemandCases()=>[
        new("order-where-any",["OrderByDescending","Where","Any"],"source.OrderByDescending(x=>x).Where(x=>10/(x-3)>0).Any()",null,"Bool","Scalar",string.Empty),
        new("reverse-where-any",["Reverse","Where","Any"],"source.Reverse().Where(x=>10/(x-3)>0).Any()",null,"Bool","Scalar",string.Empty),
        new("order-where-take",["OrderBy","Where","Take"],"source.OrderBy(x=>x).Where(x=>(x&1)==0).Take(1).ToArray()",null,"Int","Sequence",string.Empty),
        new("reverse-where-first",["Reverse","Where","FirstOrDefault"],"source.Reverse().Where(x=>(x&1)==0).FirstOrDefault()",null,"Int","Scalar",string.Empty),
        new("order-where-all",["OrderBy","Where","ToArray"],"source.OrderBy(x=>x).Where(x=>(x&1)==0).ToArray()",null,"Int","Sequence",string.Empty),
        new("reverse-where-all",["Reverse","Where","ToArray"],"source.Reverse().Where(x=>(x&1)==0).ToArray()",null,"Int","Sequence",string.Empty)
    ];

    [Fact,Trait("Category","FixedReference")]
    public void PreservesFixedReferenceEvaluationCounts(){
        var records=new List<object>();
        var failures=new List<string>();
        foreach(var item in Core.FixedCaseCatalog.All.Concat(PredicateDemandCases())){
            var source=EvaluationProbeRewriter.Instrument(Core.FixedCaseCatalog.Emit(item));
            var compilation=new DynamicQueryCompiler().Compile(new("counted-fixed-"+Guid.NewGuid().ToString("N"),"Benchmark.Dynamic.Generated.FixedQuery",source,item.ID+"-counted.cs"));
            Assert.True(compilation.Success,item.ID+": "+string.Join("\n",compilation.Diagnostics.Where(d=>d.Severity=="Error").Select(d=>d.Message)));
            SaveEvidence(item,compilation,"counted",source);
            var loaded=compilation.LoadedQuery!;
            try{
                foreach(var count in new[]{0,1,16}){
                    using var fixture=new SourceFixtureFactory().Create(SourceTarget.SystemArray,count,Seed256.Parse("42"));
                    var counts=new Dictionary<DynamicImplementationKind,long[]>();
                    var exceptions=new Dictionary<DynamicImplementationKind,string?>();
                    foreach(var implementation in new[]{DynamicImplementationKind.System,DynamicImplementationKind.FLinqUnoptimized,DynamicImplementationKind.FLinq}){
                        Assert.True(loaded.Has(implementation));
                        EvaluationProbe.Reset();
                        exceptions[implementation]=null;
                        try{loaded.Run(implementation,fixture.Primary,fixture.Secondary);}
                        catch(Exception exception){exceptions[implementation]=exception.GetType().FullName;}
                        counts[implementation]=EvaluationProbe.CountsByLambda;
                    }
                    records.Add(new{item.ID,count,counts,exceptions});
                    foreach(var implementation in new[]{DynamicImplementationKind.FLinqUnoptimized,DynamicImplementationKind.FLinq}){
                        if(exceptions[implementation]!=exceptions[DynamicImplementationKind.System]){
                            failures.Add(item.ID+" count="+count+" "+implementation+": expected exception "+exceptions[DynamicImplementationKind.System]+", actual "+exceptions[implementation]);
                        }
                        var expected=counts[DynamicImplementationKind.System];
                        var actual=counts[implementation];
                        for(var lambda=0;lambda<Math.Max(expected.Length,actual.Length);lambda++){
                            var expectedCount=0L;
                            var actualCount=0L;
                            if(lambda<expected.Length){expectedCount=expected[lambda];}
                            if(lambda<actual.Length){actualCount=actual[lambda];}
                            if(actualCount>expectedCount){failures.Add(item.ID+" count="+count+" "+implementation+" lambda#"+lambda+": System="+expectedCount+", actual="+actualCount);}
                        }
                    }
                }
            }finally{
                File.WriteAllText(Path.Combine(Path.GetTempPath(),"fixed-reference-evaluation.json"),System.Text.Json.JsonSerializer.Serialize(records));
                Assert.True(loaded.UnloadAndVerify().Unloaded);
            }
        }
        Assert.True(failures.Count==0,string.Join("\n",failures));
    }

    [Fact,Trait("Category","FixedReference")]
    public void FixedReferenceRuntimeValidationHasNoFallbacks(){
        var implementations=new[]{DynamicImplementationKind.System,DynamicImplementationKind.FLinqUnoptimized,DynamicImplementationKind.FLinq,DynamicImplementationKind.Ideal};
        var records=new List<object>();
        var failures=new List<string>();
        var evidencePath=Path.Combine(Path.GetTempPath(),"fixed-reference-runtime.json");
        foreach(var item in Core.FixedCaseCatalog.All){
            LoadedDynamicQuery? loaded=null;
            try{
                var compilation=new DynamicQueryCompiler().Compile(new("runtime-fixed-"+Guid.NewGuid().ToString("N"),"Benchmark.Dynamic.Generated.FixedQuery",Core.FixedCaseCatalog.Emit(item),item.ID+"-runtime.cs"));
                if(!compilation.Success){
                    failures.Add(item.ID+": compilation failed: "+string.Join("; ",compilation.Diagnostics.Where(diagnostic=>diagnostic.Severity=="Error").Select(diagnostic=>diagnostic.Message)));
                    continue;
                }
                SaveEvidence(item,compilation,"runtime");
                loaded=compilation.LoadedQuery!;
                foreach(var count in new[]{0,1,16}){
                    using var fixture=new SourceFixtureFactory().Create(SourceTarget.SystemArray,count,Seed256.Parse("42"));
                    var missing=implementations.Where(implementation=>!loaded.Has(implementation)).ToArray();
                    if(missing.Length!=0){failures.Add(item.ID+" count="+count+": missing implementations "+string.Join(",",missing));}
                    var result=global::Benchmark.Dynamic.Worker.DynamicQueryValidator.Validate(loaded,fixture.Primary,fixture.Secondary,implementations,true);
                    records.Add(new{caseID=item.ID,count,status=result.Status.ToString(),exceptions=result.Exceptions,comparisons=result.Comparisons,fallbacks=result.Fallbacks});
                    if(result.Status!=WorkerExecutionStatus.Succeeded){failures.Add(item.ID+" count="+count+": status="+result.Status);}
                    if(!result.ShouldMeasure){failures.Add(item.ID+" count="+count+": ShouldMeasure=false");}
                    if(result.Exceptions.Count!=0){failures.Add(item.ID+" count="+count+": unexpected exceptions "+string.Join(",",result.Exceptions.Select(entry=>entry.Key+"="+entry.Value.Type)));}
                    if(result.Comparisons.Count!=implementations.Length-1||result.Comparisons.Values.Any(comparison=>!comparison.IsMatch)){failures.Add(item.ID+" count="+count+": comparisons incomplete or mismatched");}
                    if(result.Fallbacks.Count!=0){failures.Add(item.ID+" count="+count+": fallbacks "+string.Join(",",result.Fallbacks.Select(entry=>entry.Key+"="+entry.Value)));}
                    File.WriteAllText(evidencePath,System.Text.Json.JsonSerializer.Serialize(records));
                }
            }catch(Exception exception){
                failures.Add(item.ID+": "+exception.GetType().Name+": "+exception.Message);
            }finally{
                if(loaded is not null&&!loaded.UnloadAndVerify().Unloaded){failures.Add(item.ID+": collectible query assembly was retained.");}
            }
        }
        Assert.True(failures.Count==0,string.Join("\n",failures));
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
    public void GroupSumReferencePreservesOverflowAndKeyAddition(){
        var item=Core.FixedCaseCatalog.All.Single(item=>item.ID=="chain-08");
        var compilation=new DynamicQueryCompiler().Compile(new("ideal-boundaries","Benchmark.Dynamic.Generated.FixedQuery",Core.FixedCaseCatalog.Emit(item),"ideal-boundaries.cs"));
        Assert.True(compilation.Success,string.Join("\n",compilation.Diagnostics.Select(d=>d.Message)));
        var loaded=compilation.LoadedQuery!;
        try{
            foreach(var values in new[]{Array.Empty<int>(),new[]{int.MaxValue,int.MaxValue},new[]{int.MaxValue},new[]{int.MinValue},new[]{-1,-1,0,15},new[]{int.MaxValue,-1,int.MaxValue}}){
                var expected=Capture(loaded,DynamicImplementationKind.System,values);
                var actual=Capture(loaded,DynamicImplementationKind.Ideal,values);
                Assert.Equal(expected.Exception,actual.Exception);
                if(expected.Value is not null){Assert.True(DynamicQueryComparer.Compare(expected.Value,actual.Value!).IsMatch);}
            }
        }finally{loaded.UnloadAndVerify();}
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static (DynamicQueryValue? Value,Type? Exception) Capture(LoadedDynamicQuery query,DynamicImplementationKind implementation,int[] values){
        try{return(query.Run(implementation,values,Array.Empty<int>()),null);}
        catch(Exception exception){return(null,exception.GetType());}
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
