using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Worker;

namespace Benchmark.Dynamic.Tests;

[Collection(DynamicWorkerTestCollection.Name)]
public sealed class FixedCaseCatalog{
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
