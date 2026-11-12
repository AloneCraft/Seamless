using Test.SourceGenerator;
using Seamless.FLinq;

namespace Benchmark.Dynamic.Tests;

[LinqIntercept(false)]
public sealed class CoreRangeSetCountTests{
    [Theory]
    [InlineData("inside",5)]
    [InlineData("outside",6)]
    [InlineData("static",4)]
    [InlineData("boxed",4)]
    public void UnionCountKeepsTheSetBoundaryAndResultType(string mode,int expected){
        var expression=mode switch{
            "inside"=>"Enumerable.Range(0,3).Union(Enumerable.Range(1,3).Concat(Enumerable.Range(2,3))).Count()",
            "outside"=>"Enumerable.Range(0,3).Concat(Enumerable.Range(0,3)).Count()",
            "static"=>"Enumerable.Count(Enumerable.Union(Enumerable.Range(0,3),Enumerable.Range(1,3)))",
            _=>"Enumerable.Range(0,3).Union(Enumerable.Range(1,3)).Count()"
        };
        var source=$$"""
            using System;
            using System.Linq;
            using Seamless.FLinq;
            namespace RangeCount;
            public sealed class Probe{
                [LinqIntercept(true)]
                private object Query()=>{{expression}};
                public object Run()=>this.Query();
            }
            """;
        var output=ExecutableGeneration.Generate(source,"RangeCount_"+mode,1);
        var result=ExecutableGeneration.Run(output.Compilation,"RangeCount.Probe",type=>type.GetMethod("Run")!.Invoke(Activator.CreateInstance(type),null));
        Assert.Equal(expected,Assert.IsType<int>(result));
    }
}
