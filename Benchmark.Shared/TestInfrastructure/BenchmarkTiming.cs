#if BENCH_AS_TEST
using System.Diagnostics;
using System.Reflection;
using Xunit.Sdk;

[assembly:Benchmark.Shared.BenchmarkTiming]

namespace Benchmark.Shared;
internal sealed class BenchmarkTimingAttribute:BeforeAfterTestAttribute{
    public override void Before(MethodInfo methodUnderTest){
        if(methodUnderTest.GetCustomAttribute<BenchmarkAttribute>()==null){
            return;
        }
        var declaringType=methodUnderTest.DeclaringType;
        if(declaringType==null){
            return;
        }
        var targetType=declaringType;
        var reflectedType=methodUnderTest.ReflectedType;
        if(reflectedType!=null&&!reflectedType.IsAbstract){
            targetType=reflectedType;
        }
        if(targetType==null||targetType.IsAbstract){
            return;
        }
        var instance=Activator.CreateInstance(targetType);
        for(var i=0;i<BenchmarkParameters.WarmupCount;i++){
            methodUnderTest.Invoke(instance,null);
        }
        var testIterations=targetType.GetCustomAttribute<BenchmarkTestIterationsAttribute>()?.Count??1;
        var runCount=BenchmarkParameters.RunCount*testIterations;
        var sw=Stopwatch.StartNew();
        for(var i=0;i<runCount;i++){
            methodUnderTest.Invoke(instance,null);
        }
        sw.Stop();
        var totalMs=sw.Elapsed.TotalMilliseconds;
        Console.WriteLine($"{targetType.Name}.{methodUnderTest.Name}: {runCount} run(s) total {totalMs:F3} ms avg {totalMs/runCount:F4} ms");
    }
}
#endif
