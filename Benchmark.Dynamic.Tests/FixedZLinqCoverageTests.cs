using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Worker;

namespace Benchmark.Dynamic.Tests;

[Collection(DynamicWorkerTestCollection.Name)]
public sealed class FixedZLinqCoverageTests{
    [Fact]
    public void 全固定ケースのZLinq呼出先と結果が一致する(){
        var errors=new List<string>();
        var values=Enumerable.Range(0,16).Select(i=>i-4).ToArray();
        var second=new[]{1,2,3};
        foreach(var item in Benchmark.Dynamic.Core.FixedCaseCatalog.All){
            if(item.ZLinqExpression is null){errors.Add(item.ID+": ZLinq expression missing");continue;}
            var compiled=new DynamicQueryCompiler().Compile(new("zlinq-"+Guid.NewGuid().ToString("N"),"Benchmark.Dynamic.Generated.FixedQuery",Benchmark.Dynamic.Core.FixedCaseCatalog.Emit(item),item.ID+".cs"));
            if(!compiled.Success){errors.Add(item.ID+": "+string.Join("; ",compiled.Diagnostics.Where(d=>d.Severity=="Error").Select(d=>d.Message)));continue;}
            var loaded=compiled.LoadedQuery!;
            try{
                var expected=loaded.Run(DynamicImplementationKind.System,values,second);
                var actual=loaded.Run(DynamicImplementationKind.ZLinq,values,second);
                if(!DynamicQueryComparer.Compare(expected,actual).IsMatch){errors.Add(item.ID+": result mismatch");}
            }catch(Exception exception){errors.Add(item.ID+": "+exception.Message);}
            finally{if(!loaded.UnloadAndVerify().Unloaded){errors.Add(item.ID+": unload failed");}}
        }
        Assert.True(errors.Count==0,string.Join("\n",errors));
    }
}
