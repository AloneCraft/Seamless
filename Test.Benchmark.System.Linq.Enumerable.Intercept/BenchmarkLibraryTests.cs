using System.Reflection;
using System.Runtime.ExceptionServices;
using Benchmark.Shared;

namespace Test.Benchmark.System.Linq.Enumerable.Intercept;

public sealed class BenchmarkLibraryTests {
    public static IEnumerable<object[]> UnitTests() {
        BenchmarkExecutionMode.EnableTestMode();

        return typeof(global::Benchmark.System.Linq.Enumerable.Intercept.Any).Assembly
            .GetTypes()
            .Where(static type => !type.IsAbstract)
            .SelectMany(static type => type
                .GetMethods(BindingFlags.Instance|BindingFlags.Public)
                .Where(HasUnitFact)
                .Select(method => new BenchmarkUnitTestCase(type,method)))
            .OrderBy(static testCase => testCase.Priority)
            .ThenBy(static testCase => testCase.DisplayName,StringComparer.Ordinal)
            .Select(static testCase => new object[] { testCase.TypeName,testCase.MethodName })
            .ToArray();
    }

    [Xunit.Theory]
    [Xunit.MemberData(nameof(UnitTests))]
    public void Results_match(string typeName,string methodName) {
        BenchmarkExecutionMode.EnableTestMode();
        var type = typeof(global::Benchmark.System.Linq.Enumerable.Intercept.Any).Assembly.GetType(typeName,true)!;
        var method = type.GetMethod(methodName,BindingFlags.Instance|BindingFlags.Public)
            ??throw new InvalidOperationException($"Could not find benchmark test method: {typeName}.{methodName}");

        Invoke(type,method);
    }

    private static bool HasUnitFact(MethodInfo method) =>
        method.GetCustomAttribute<global::Benchmark.Shared.FactAttribute>() is not null
        &&method.GetCustomAttribute<BenchFactAttribute>() is null;

    private static void Invoke(Type type,MethodInfo method) {
        var instance = Activator.CreateInstance(type)
            ??throw new InvalidOperationException($"Could not create benchmark type: {type.FullName}");

        try {
            method.Invoke(instance,null);
        }
        catch(TargetInvocationException ex) when(ex.InnerException is not null) {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }
    }

    private sealed class BenchmarkUnitTestCase(Type type,MethodInfo method) {
        public string TypeName { get; } = type.FullName??type.Name;
        public string MethodName { get; } = method.Name;
        public string DisplayName { get; } = $"{type.Name}.{method.Name}";
        public int Priority { get; } = method.GetCustomAttribute<TestPriorityAttribute>()?.Priority??0;
    }
}
