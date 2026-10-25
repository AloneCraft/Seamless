using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Worker;

namespace Benchmark.Dynamic.Tests;

[Collection(DynamicWorkerTestCollection.Name)]
public sealed class LegacyChainCatalog{
    [Fact,Trait("Category","LegacyChainCorpus")]
    public void ExactSavedRegressionCasesPreserveArgumentsAndResults(){
        foreach(var file in new[]{"representative-chains.json","regressions.json"}){
            var corpus=CorpusChainBuilder.Load(file);
            foreach(var entry in corpus.Entries){
                Verify(Core.LegacyChainCatalog.Build(entry.Id,entry.Methods.ToArray()));
            }
        }
    }
    [Fact,Trait("Category","LegacyChainCorpus")]
    public void ExactLegacyPairsPreserveArgumentsAndResults(){
        var cases=Core.LegacyChainCatalog.Pairs().ToArray();Assert.True(cases.Length>=150);
        foreach(var item in cases){Verify(item);}
    }
    private static void Verify(FixedCase item){
            var compilation=new DynamicQueryCompiler().Compile(new("legacy-"+Guid.NewGuid().ToString("N"),"Benchmark.Dynamic.Generated.FixedQuery",Core.FixedCaseCatalog.Emit(item),item.ID+".cs"));
            Assert.True(compilation.Success,item.ID+": "+string.Join("\n",compilation.Diagnostics.Where(d=>d.Severity=="Error").Select(d=>d.Message)));
            var loaded=compilation.LoadedQuery!;
            try{
                foreach(var count in new[]{0,1,16}){
                    using var fixture=new SourceFixtureFactory().Create(SourceTarget.SystemArray,count,Seed256.Parse("42"));
                    var (expected,expectedException)=Execute(loaded,DynamicImplementationKind.System,fixture.Primary,fixture.Secondary);
                    Assert.NotEqual(typeof(InvalidCastException).FullName,expectedException);
                    foreach(var implementation in new[]{DynamicImplementationKind.FLinqUnoptimized,DynamicImplementationKind.FLinq}){
                        var (actual,actualException)=Execute(loaded,implementation,fixture.Primary,fixture.Secondary);
                        Assert.Equal(expectedException,actualException);
                        if(expected is not null){Assert.True(DynamicQueryComparer.Compare(expected,actual!).IsMatch,item.ID+" "+implementation+" count="+count);}
                    }
                }
            }finally{Assert.True(loaded.UnloadAndVerify().Unloaded,item.ID+": collectible query assembly was retained.");}
    }
    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static (DynamicQueryValue? Value,string? Exception) Execute(LoadedDynamicQuery query,DynamicImplementationKind implementation,object primary,object secondary){
        try{return(query.Run(implementation,primary,secondary),null);}
        catch(Exception exception){return(null,exception.GetType().FullName);}
    }
}
