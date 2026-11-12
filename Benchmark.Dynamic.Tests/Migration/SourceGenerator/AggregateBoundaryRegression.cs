using FactAttribute=Benchmark.Dynamic.Worker.RegressionContractAttribute;
using Seamless.FLinq;

namespace Test.SourceGenerator;

[LinqIntercept(false)]
public sealed class AggregateBoundaryRegression {
    [Fact]
    public void 集計追加要素と逆順保持() {
        const string source="""
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public sealed class Probe {
                private static readonly decimal[] Values = [-1m, 2m, 3m];
                [LinqIntercept(true, Optimizations=LinqOptimizations.All, ConvergenceMode=LinqConvergenceMode.GlobalFixedPoint)]
                public static long Append() => Values.Append(-60m).Aggregate(0L,(a,v)=>a+(long)v.GetHashCode());
                [LinqIntercept(true, Optimizations=LinqOptimizations.All, ConvergenceMode=LinqConvergenceMode.GlobalFixedPoint)]
                public static long Prepend() => Values.Prepend(-60m).Aggregate(0L,(a,v)=>a*3+(long)v.GetHashCode());
                [LinqIntercept(true, Optimizations=LinqOptimizations.All, ConvergenceMode=LinqConvergenceMode.GlobalFixedPoint)]
                public static long Reverse() => Values.Reverse().Aggregate(0L,(a,v)=>a*3+(long)v.GetHashCode());
                public static long[] Actual() => [Append(), Prepend(), Reverse()];
                [LinqIntercept(false)]
                public static long[] Expected() => [
                    Values.Append(-60m).Aggregate(0L,(a,v)=>a+(long)v.GetHashCode()),
                    Values.Prepend(-60m).Aggregate(0L,(a,v)=>a*3+(long)v.GetHashCode()),
                    Values.Reverse().Aggregate(0L,(a,v)=>a*3+(long)v.GetHashCode())];
            }
            """;
        var (compilation,_)=ExecutableGeneration.Generate(source,"AggregateBoundaryRegression",3);
        var (actual,expected)=ExecutableGeneration.Run(compilation,"Sample.Probe",probe=>(
            (long[])probe.GetMethod("Actual")!.Invoke(null,null)!,
            (long[])probe.GetMethod("Expected")!.Invoke(null,null)!));
        Assert.Equal(expected,actual);
    }
}
