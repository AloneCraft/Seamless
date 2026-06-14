using System.Diagnostics;
using System.Reflection;
using Xunit.v3;

[assembly:Seamless.Benchmarks.BenchmarkTiming]

namespace Seamless.Benchmarks;

// [Benchmark] が付いたテストメソッドだけを対象に、RunCount 回実行して経過時間を計測しテスト出力へ記録する。
// アセンブリ全体に適用するが、[Benchmark] が無いテスト（通常のUnit等）には即座に何もしない。
sealed class BenchmarkTimingAttribute:BeforeAfterTestAttribute{
    public override void Before(MethodInfo methodUnderTest,IXunitTest test){
        if(methodUnderTest.GetCustomAttribute<BenchmarkAttribute>()==null){
            return;
        }
        var declaringType=methodUnderTest.DeclaringType;
        if(declaringType==null){
            return;
        }
        var instance=Activator.CreateInstance(declaringType);
        for(var i=0;i<BenchmarkParameters.WarmupCount;i++){
            methodUnderTest.Invoke(instance,null);
        }
        var testIterations=declaringType.GetCustomAttribute<BenchmarkTestIterationsAttribute>()?.Count??1;
        var runCount=BenchmarkParameters.RunCount*testIterations;
        var sw=Stopwatch.StartNew();
        for(var i=0;i<runCount;i++){
            methodUnderTest.Invoke(instance,null);
        }
        sw.Stop();
        var totalMs=sw.Elapsed.TotalMilliseconds;
        var line=$"{declaringType.Name}.{methodUnderTest.Name}: {runCount} run(s) total {totalMs:F3} ms avg {totalMs/runCount:F4} ms";
        var output=TestContext.Current.TestOutputHelper;
        if(output!=null){
            output.WriteLine(line);
        }else{
            TestContext.Current.SendDiagnosticMessage(line);
        }
    }
}
