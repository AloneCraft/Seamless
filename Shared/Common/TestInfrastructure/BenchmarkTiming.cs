#if BENCH_AS_TEST
using System.Diagnostics;
using System.Reflection;

using Xunit.Sdk;

[assembly: Common.BenchmarkTiming]

namespace Common;

internal sealed class BenchmarkTimingAttribute:BeforeAfterTestAttribute {
    public override void Before(MethodInfo methodUnderTest) {
        if(methodUnderTest.GetCustomAttribute<BenchmarkAttribute>()==null) {
            return;
        }
        var declaringType = methodUnderTest.DeclaringType;
        if(declaringType==null) {
            return;
        }
        var targetType = declaringType;
        var reflectedType = methodUnderTest.ReflectedType;
        if(reflectedType!=null&&!reflectedType.IsAbstract) {
            targetType=reflectedType;
        }
        if(targetType==null||targetType.IsAbstract) {
            return;
        }
        if(methodUnderTest.GetParameters().Length!=0) {
            return;
        }
        var instance = Activator.CreateInstance(targetType);
        for(var i = 0;i<BenchmarkParameters.WarmupCount;i++) {
            InvokeToCompletion(methodUnderTest,instance);
        }
        var testIterations = targetType.GetCustomAttribute<BenchmarkTestIterationsAttribute>()?.Count??1;
        var runCount = BenchmarkParameters.RunCount*testIterations;
        var sw = Stopwatch.StartNew();
        for(var i = 0;i<runCount;i++) {
            InvokeToCompletion(methodUnderTest,instance);
        }
        sw.Stop();
        var totalMs = sw.Elapsed.TotalMilliseconds;
        Console.WriteLine($"{targetType.Name}.{methodUnderTest.Name}: {runCount} run(s) total {totalMs:F3} ms avg {totalMs/runCount:F4} ms");
    }

    // 非同期のテストメソッドは Task を返した時点で戻り、本体の続きは後で走る。待たずに次の呼び出しや本来のテストへ進むと、
    // 同じテストが同じフォルダを並行して使い、ファイルの共有違反になる(BUG-0006)。
    // 同期コンテキストを持たないスレッドプールで完了まで待ち、テスト実行側のスレッドを塞いでもデッドロックしないようにする。
    private static void InvokeToCompletion(MethodInfo method,object? instance) {
        if(!typeof(Task).IsAssignableFrom(method.ReturnType)&&method.ReturnType!=typeof(ValueTask)) {
            method.Invoke(instance,null);
            return;
        }
        Task.Run(async () => {
            var result = method.Invoke(instance,null);
            if(result is Task task) {
                await task;
            } else if(result is ValueTask valueTask) {
                await valueTask;
            }
        }).GetAwaiter().GetResult();
    }
}
#endif
