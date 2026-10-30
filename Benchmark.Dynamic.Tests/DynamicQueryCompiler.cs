using Benchmark.Dynamic.Core;

namespace Benchmark.Dynamic.Tests.Compilation;

[Collection(DynamicWorkerTestCollection.Name)]
public sealed class DynamicQueryCompiler{
    [Fact,Trait("Category","FixedReference")]
    public void CompileChecksTheActualInterceptorAtEachEntryCall(){
        var item=Core.FixedCaseCatalog.All[0];
        var source=Core.FixedCaseCatalog.Emit(item);
        var compiler=new global::Benchmark.Dynamic.Worker.DynamicQueryCompiler();
        var valid=compiler.Compile(new("bound-"+Guid.NewGuid().ToString("N"),"Benchmark.Dynamic.Generated.FixedQuery",source,"bound.cs"));
        try{
            Assert.True(valid.Success,string.Join("\n",valid.Diagnostics));
            Assert.Equal(2,valid.Diagnostics.Count(diagnostic=>diagnostic.ID=="FLinqInterceptorBound"));
        }finally{if(valid.LoadedQuery is { } loaded){Assert.True(loaded.UnloadAndVerify().Unloaded);}}

        // 同型の補助呼出と、要求型以外の同名入口は傍受必須のCoreに含めない。
        var helperSource=source.Replace("RunFLinq(object? primary,object? secondary)=>RunFLinqCore(primary,secondary);",
            "RunFLinq(object? primary,object? secondary){BeforeEntry();return RunFLinqCore(primary,secondary);}",StringComparison.Ordinal);
        helperSource=helperSource.Insert(helperSource.LastIndexOf('}'),"private static void BeforeEntry(){}");
        helperSource+="internal static class UnrelatedEntries{public static int RunFLinq()=>0;}";
        Assert.Contains("BeforeEntry();return RunFLinqCore(primary,secondary);",helperSource,StringComparison.Ordinal);
        var withHelper=compiler.Compile(new("helper-"+Guid.NewGuid().ToString("N"),"Benchmark.Dynamic.Generated.FixedQuery",helperSource,"helper.cs"));
        try{
            Assert.True(withHelper.Success,string.Join("\n",withHelper.Diagnostics));
            Assert.Equal(2,withHelper.Diagnostics.Count(diagnostic=>diagnostic.ID=="FLinqInterceptorBound"));
        }finally{if(withHelper.LoadedQuery is { } loaded){Assert.True(loaded.UnloadAndVerify().Unloaded);}}

        // 他の生成メソッド名に両Core名が含まれても、無効化した呼出位置は傍受されていない。
        var invalidSource=source.Replace("RunFLinqUnoptimizedCore","RunFLinqUnoptimizedCore_RunFLinqCore",StringComparison.Ordinal)
            .Replace("public static global::Benchmark.Dynamic.Core.DynamicQueryValue RunFLinq(","[global::Seamless.FLinq.LinqIntercept(false)]public static global::Benchmark.Dynamic.Core.DynamicQueryValue RunFLinq(",StringComparison.Ordinal);
        var invalid=compiler.Compile(new("unbound-"+Guid.NewGuid().ToString("N"),"Benchmark.Dynamic.Generated.FixedQuery",invalidSource,"unbound.cs"));
        AssertRejected(invalid,"FLinqInterceptorMissing","RunFLinqの",true);

        // 両入口が実際に傍受されても、最適化入口を未最適化Coreへ配線したものは拒否する。
        var misroutedSource=source.Replace("RunFLinqCore(primary,secondary)","RunFLinqUnoptimizedCore(primary,secondary)",StringComparison.Ordinal);
        Assert.Contains("RunFLinq(object? primary,object? secondary)=>RunFLinqUnoptimizedCore(primary,secondary);",misroutedSource,StringComparison.Ordinal);
        var misrouted=compiler.Compile(new("misrouted-"+Guid.NewGuid().ToString("N"),"Benchmark.Dynamic.Generated.FixedQuery",misroutedSource,"misrouted.cs"));
        AssertRejected(misrouted,"InvocationTargetMismatch","RunFLinqは",false);

        var ambiguousSource=source.Insert(source.LastIndexOf('}'),"private static global::Benchmark.Dynamic.Core.DynamicQueryValue RunFLinqCore(object? primary)=>RunSystem(primary,null);");
        var ambiguous=compiler.Compile(new("ambiguous-"+Guid.NewGuid().ToString("N"),"Benchmark.Dynamic.Generated.FixedQuery",ambiguousSource,"ambiguous.cs"));
        AssertRejected(ambiguous,"FLinqInterceptorMissing","RunFLinqの正規Core",false);
    }

    private static void AssertRejected(global::Benchmark.Dynamic.Worker.DynamicCompilationResult result,string diagnosticID,string message,bool provesLegacyCondition){
        try{
            if(provesLegacyCondition){
                Assert.Contains("RunFLinqUnoptimizedCore",result.ExpandedSource,StringComparison.Ordinal);
                Assert.Contains("RunFLinqCore",result.ExpandedSource,StringComparison.Ordinal);
            }
            Assert.False(result.Success);
            Assert.Null(result.LoadedQuery);
            Assert.Contains(result.Diagnostics,diagnostic=>diagnostic.ID==diagnosticID&&diagnostic.Message.Contains(message,StringComparison.Ordinal));
        }finally{if(result.LoadedQuery is { } loaded){Assert.True(loaded.UnloadAndVerify().Unloaded);}}
    }
}
