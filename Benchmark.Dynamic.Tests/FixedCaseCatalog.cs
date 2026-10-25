using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Worker;

namespace Benchmark.Dynamic.Tests;

[Collection(DynamicWorkerTestCollection.Name)]
public sealed class FixedCaseCatalog{
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
