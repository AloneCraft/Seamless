using Benchmark.Dynamic.Worker;

namespace Benchmark.Dynamic.Tests;

public sealed class StaticBuildContracts{
    [Theory]
    [InlineData(0,"StaticChain",12)]
    [InlineData(1,"StaticGeneric",10)]
    [InlineData(2,"StaticTyped",40)]
    public void ActualMsBuild_expands_and_executes_without_fallback(int variant,string method,int expected){
        var assembly=typeof(StaticBuildContracts).Assembly;
        Assert.Contains(assembly.GetTypes().Where(type=>type.Namespace?.StartsWith("Intercept.",StringComparison.Ordinal)==true).SelectMany(type=>type.GetMethods()),item=>item.Name.StartsWith("OptimizedLinq_"+method+"_",StringComparison.Ordinal));
        var observation=DynamicFallbackCapture.Run(()=>variant switch{
            0=>StaticBuildFixture.RunChain([1,2,3,4]),
            1=>StaticGenericFixture<int>.Run([1,2,3,4],static value=>value),
            2=>StaticBuildFixture.RunTyped([new(1,10),new(2,20),new(1,30)]),
            _=>throw new ArgumentOutOfRangeException(nameof(variant)),
        });
        Assert.Null(observation.Exception);Assert.Equal(0,observation.FallbackCount);
        Assert.Equal(expected,observation.Value);
    }
}

[global::Seamless.FLinq.LinqIntercept(true)]
internal sealed partial class StaticBuildFixture{
    internal readonly record struct Row(int Key,int Value);
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int StaticChain(int[] values)=>values.Where(static value=>(value&1)==0).Select(static value=>value*2).Sum();
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int StaticTyped(Row[] values)=>values.Where(static value=>value.Key==1).Sum(static value=>value.Value);
}

[global::Seamless.FLinq.LinqIntercept(true)]
internal sealed class StaticGenericFixture<T>{
    internal static int Run(T[] values,Func<T,int> selector)=>StaticGeneric(values,selector);
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int StaticGeneric(T[] values,Func<T,int> selector)=>values.Select(selector).Sum();
}
