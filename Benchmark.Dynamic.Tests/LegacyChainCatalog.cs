using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Worker;
using System.Text.Json;

namespace Benchmark.Dynamic.Tests;

[Collection(DynamicWorkerTestCollection.Name)]
public sealed class LegacyChainCatalog{
    [Fact,Trait("Category","LegacyChainCorpus")]
    public void ExactSavedRegressionCasesPreserveArgumentsAndResults(){
        var executed=0;
        foreach(var file in new[]{"representative-chains.json","regressions.json"}){
            var corpus=CorpusChainBuilder.Load(file);
            foreach(var entry in corpus.Entries){
                Verify(Core.LegacyChainCatalog.Build(entry.Id,entry.Methods.ToArray()),"saved");
                executed++;
            }
        }
        Assert.Equal(225,executed);
    }
    [Fact,Trait("Category","LegacyChainCorpus")]
    public void ExactLegacyPairsPreserveArgumentsAndResults(){
        var cases=Core.LegacyChainCatalog.Pairs().ToArray();Assert.True(cases.Length>=150);
        foreach(var item in cases){Verify(item,"pairs");}
    }
    private static void Verify(FixedCase item,string phase){
            var source=Core.FixedCaseCatalog.Emit(item);
            var compilation=new DynamicQueryCompiler().Compile(new("legacy-"+Guid.NewGuid().ToString("N"),"Benchmark.Dynamic.Generated.FixedQuery",source,item.ID+".cs"));
            var directory=string.Empty;
            var validations=new List<object>();
            var loaded=compilation.LoadedQuery;
            try{
                directory=Path.Combine(Path.GetTempPath(),"LegacyOriginalEvidence",phase);
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory,item.ID+".source.cs"),source);
                File.WriteAllText(Path.Combine(directory,item.ID+".generated.cs"),compilation.ExpandedSource);
                SaveEvidence();
                Assert.True(compilation.Success,item.ID+": "+string.Join("\n",compilation.Diagnostics.Where(d=>d.Severity=="Error").Select(d=>d.Message)));
                var query=Assert.IsType<LoadedDynamicQuery>(loaded);
                foreach(var count in new[]{0,1,16}){
                    using var fixture=new SourceFixtureFactory().Create(SourceTarget.SystemArray,count,Seed256.Parse("42"));
                    var validation=global::Benchmark.Dynamic.Worker.DynamicQueryValidator.Validate(query,fixture.Primary,fixture.Secondary,[DynamicImplementationKind.System,DynamicImplementationKind.FLinqUnoptimized,DynamicImplementationKind.FLinq],true);
                    validations.Add(new{count,Status=validation.Status.ToString(),validation.Exceptions,validation.Comparisons,validation.Fallbacks});
                    if(validation.Exceptions.TryGetValue(DynamicImplementationKind.System,out var failure)){
                        Assert.NotEqual(typeof(InvalidCastException).FullName,failure.Type);
                    }
                    Assert.True(validation.Status==WorkerExecutionStatus.Succeeded,item.ID+" count="+count+": "+JsonSerializer.Serialize(validation));
                }
            }finally{
                try{SaveEvidence();}
                finally{if(loaded is not null){Assert.True(loaded.UnloadAndVerify().Unloaded,item.ID+": collectible query assembly was retained.");}}
            }
            void SaveEvidence(){
                if(directory.Length==0){return;}
                File.WriteAllText(Path.Combine(directory,item.ID+".json"),JsonSerializer.Serialize(new{
                    item.ID,item.Methods,item.Expression,item.OriginalSHA256,
                    SourceSHA256=Benchmark.Infrastructure.Fingerprint.Of(source),
                    GeneratedSHA256=Benchmark.Infrastructure.Fingerprint.Of(compilation.ExpandedSource),
                    compilation.Success,compilation.Diagnostics,compilation.OptimizationTracePayloads,Validation=validations
                }));
            }
    }
}
