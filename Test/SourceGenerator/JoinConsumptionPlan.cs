using Seamless.FLinq.Generator;

namespace Test.SourceGenerator;

public sealed class JoinConsumptionPlan{
    [Fact]public void ExecutionAndTrace(){
        const string source="""
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public sealed class Probe{
                [LinqIntercept(true,EmitOptimizationTrace=true)]
                private static int Count(int[] outer,int[] inner)=>outer.Join(inner,x=>x,x=>x,(a,b)=>a+b).Count();
                public static int Run()=>Count(new[]{1,2,2},new[]{2,2});
            }
            """;
        var (compilation,generated)=ExecutableGeneration.Generate(source,"JoinConsumptionExecution",1);
        const string prefix="// FLinqOptimizationTrace:";
        var traces=string.Join("\n",generated.Split('\n').Where(line=>line.StartsWith(prefix,StringComparison.Ordinal))
            .Select(line=>global::System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(line.Substring(prefix.Length).Trim()))));
        Assert.Contains("JoinConsumptionPlan",traces);
        Assert.Equal(4,ExecutableGeneration.Run(compilation,"Sample.Probe",probe=>(int)probe.GetMethod("Run")!.Invoke(null,null)!));
    }
    [Fact]public void Create(){
        var demand=DemandAnalysis.Analyze([],new(PipelineTerminalKind.Count,false,false)).Source;
        var plan=global::Seamless.FLinq.Generator.JoinConsumptionPlan.Create(demand,false,true,true);
        Assert.False(plan.OutputValuesRequired);Assert.Equal(ResultEvaluation.Preserve,plan.Evaluation);
        Assert.Equal(JoinLookupKind.KeyCounts,plan.Lookup);Assert.False(plan.RequiresLookupDispose);Assert.Contains("Anchor",plan.Reason);
        var elements=global::Seamless.FLinq.Generator.JoinConsumptionPlan.Create(demand,false,false,false);
        Assert.Equal(JoinLookupKind.Elements,elements.Lookup);Assert.True(elements.RequiresLookupDispose);
    }
}
