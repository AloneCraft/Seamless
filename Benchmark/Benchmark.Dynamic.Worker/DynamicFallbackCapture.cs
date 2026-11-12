using Seamless.Linq;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Benchmark.Dynamic.Tests")]

namespace Benchmark.Dynamic.Worker;

internal static class DynamicFallbackCapture{
    public static DynamicFallbackResult<T> Run<T>(Func<T> action){
        using var observation=LinqFallbackObservation.Start();
        using var scope=LinqCounterScope.Start();
        try{
            var value=action();
            return new(value,null,observation.Count);
        }catch(Exception exception) when(exception is not OperationCanceledException){
            return new(default,exception,observation.Count);
        }
    }
}

internal sealed record DynamicFallbackResult<T>(T? Value,Exception? Exception,long FallbackCount);
