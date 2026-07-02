using System.Reflection;
using System.Runtime.Loader;
using System.Text;

namespace Test.System.Linq.BenchmarkValueComparison;

public sealed class BenchmarkValueComparisonTests {
    private const int ComparisonIterationCount = 3;
    private const string BenchmarkExecutionModeTypeName = "Common.BenchmarkExecutionMode";
    private const string ElementCountEnvironmentVariable = "SEAMLESS_BENCHMARK_ELEMENT_COUNT";
    private const string Methods1TypeNamePrefix = "Seamless.Linq.Shared.Methods1`1";

    [Fact]
    [global::Seamless.FLinq.LinqIntercept(false)]
    public void Methods1_results_match() {
        this.AssertMethods1ResultsMatch("Int32");
        this.AssertMethods1ResultsMatch("String");
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private void AssertMethods1ResultsMatch(string typeSuffix) {
        var flinq=BenchmarkTarget.Load("Test.System.Linq.Enumerable.FLinq","Test.System.Linq.Enumerable.FLinq"+typeSuffix);
        var zlinq=BenchmarkTarget.Load("Test.System.Linq.Enumerable.ZLinq","Test.System.Linq.Enumerable.ZLinq"+typeSuffix);
        var targets=new[] { flinq,zlinq };
        var seedBase=Random.Shared.Next();
        var elementCountBase=Random.Shared.Next(384,2049);

        try {
            var methodNames=DiscoverMethodNames(zlinq.Type);
            var failures=new List<string>();
            for(var iteration=0;iteration<ComparisonIterationCount;iteration++) {
                var seed=unchecked(seedBase+iteration*1_000_003);
                var elementCount=elementCountBase+iteration*97;
                foreach(var target in targets) {
                    target.EnableComparisonMode(elementCount);
                }
                using var flinqSeed=flinq.UseRandomSourceSeed(seed);
                using var zlinqSeed=zlinq.UseRandomSourceSeed(seed);
                var flinqInstance=flinq.CreateInstance();
                var zlinqInstance=zlinq.CreateInstance();

                foreach(var methodName in methodNames) {
                    try {
                        var expected=zlinq.Capture(zlinqInstance,methodName,invokeImpl:false);
                        AddFailureIfDifferent(failures,methodName,"FLinq public",seed,elementCount,expected,flinq.Capture(flinqInstance,methodName,invokeImpl:false));
                        AddFailureIfDifferent(failures,methodName,"FLinq private Impl",seed,elementCount,expected,flinq.Capture(flinqInstance,methodName,invokeImpl:true));
                    } catch(Exception ex) {
                        failures.Add(methodName
                            +" / Seed="+seed
                            +" / ElementCount="+elementCount
                            +": "+ex.GetType().FullName+": "+ex.Message);
                    }
                }
            }

            if(failures.Count!=0) {
                var message=new StringBuilder();
                message.AppendLine("Benchmark value comparison failed.");
                message.AppendLine("Failure count: "+failures.Count);
                foreach(var failure in failures.Take(20)) {
                    message.AppendLine();
                    message.AppendLine(failure);
                }
                if(failures.Count>20) {
                    message.AppendLine();
                    message.AppendLine("... "+(failures.Count-20)+" more failure(s).");
                }
                throw new Xunit.Sdk.XunitException(message.ToString());
            }
        } finally {
            foreach(var target in targets) {
                target.DisableComparisonMode();
            }
        }
    }

    [Fact]
    public void BenchmarkExecutionMode_UseElementCount_IsScoped() {
        var target=BenchmarkTarget.Load("Test.System.Linq.Enumerable.ZLinq","Test.System.Linq.Enumerable.ZLinqInt32");

        Assert.Equal(128,target.ScaleElementCount(128));
        using(var scope=target.UseElementCount(7,"1")) {
            Assert.Equal(7,target.ScaleElementCount(128));
        }
        Assert.Equal(128,target.ScaleElementCount(128));
    }

    [Fact]
    public void BenchmarkExecutionMode_ElementCountEnvironment_IsReadPerCall() {
        var target=BenchmarkTarget.Load("Test.System.Linq.Enumerable.ZLinq","Test.System.Linq.Enumerable.ZLinqInt32");
        var previous=Environment.GetEnvironmentVariable(ElementCountEnvironmentVariable);
        try {
            Environment.SetEnvironmentVariable(ElementCountEnvironmentVariable,"3");
            Assert.Equal(3,target.ScaleElementCount(128));
            Environment.SetEnvironmentVariable(ElementCountEnvironmentVariable,"5");
            Assert.Equal(5,target.ScaleElementCount(128));
        } finally {
            Environment.SetEnvironmentVariable(ElementCountEnvironmentVariable,previous);
        }
    }

    private static List<string> DiscoverMethodNames(Type type) {
        var names=new List<string>();
        foreach(var method in type.GetMethods(BindingFlags.Instance|BindingFlags.Public)) {
            if(method.DeclaringType?.FullName?.StartsWith(Methods1TypeNamePrefix,StringComparison.Ordinal)!=true) {
                continue;
            }
            if(method.GetParameters().Length!=0||method.ReturnType!=typeof(void)) {
                continue;
            }
            if(!HasAttribute(method,"BenchmarkAttribute")) {
                continue;
            }

            names.Add(method.Name);
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    private static bool HasAttribute(MemberInfo member,string attributeName) {
        foreach(var attribute in member.CustomAttributes) {
            if(attribute.AttributeType.Name==attributeName) {
                return true;
            }
        }
        return false;
    }

    private static void AddFailureIfDifferent(
        List<string> failures,
        string methodName,
        string scenario,
        int seed,
        int elementCount,
        string expected,
        string actual) {
        if(StringComparer.Ordinal.Equals(expected,actual)) {
            return;
        }

        failures.Add(methodName+" / "+scenario
            +" / Seed="+seed
            +" / ElementCount="+elementCount
            +Environment.NewLine+"Expected: "+expected
            +Environment.NewLine+"Actual:   "+actual);
    }

    private static object? Invoke(MethodInfo method,object? instance,object?[]? parameters) {
        try {
            return method.Invoke(instance,parameters);
        } catch(TargetInvocationException ex) when(ex.InnerException is not null) {
            global::System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw new InvalidOperationException("Unreachable.");
        }
    }

    private sealed class BenchmarkTarget {
        private readonly Type benchmarkExecutionModeType;
        private readonly MethodInfo enableComparisonMode;
        private readonly MethodInfo disableComparisonMode;
        private readonly MethodInfo useElementCount;
        private readonly MethodInfo useRandomSourceSeed;
        private readonly MethodInfo scaleElementCount;
        private readonly MethodInfo captureResult;

        private BenchmarkTarget(Assembly assembly,Type type) {
            this.Assembly=assembly;
            this.Type=type;
            this.benchmarkExecutionModeType=assembly.GetType(BenchmarkExecutionModeTypeName,throwOnError:true)!;
            this.enableComparisonMode=this.benchmarkExecutionModeType.GetMethod(
                "EnableComparisonMode",
                BindingFlags.Static|BindingFlags.Public,
                binder:null,
                types:[typeof(int)],
                modifiers:null)!;
            this.disableComparisonMode=this.benchmarkExecutionModeType.GetMethod(
                "DisableComparisonMode",
                BindingFlags.Static|BindingFlags.Public,
                binder:null,
                types:Type.EmptyTypes,
                modifiers:null)!;
            this.useElementCount=this.benchmarkExecutionModeType.GetMethod(
                "UseElementCount",
                BindingFlags.Static|BindingFlags.Public,
                binder:null,
                types:[typeof(int),typeof(string)],
                modifiers:null)!;
            this.useRandomSourceSeed=this.benchmarkExecutionModeType.GetMethod(
                "UseRandomSourceSeed",
                BindingFlags.Static|BindingFlags.Public,
                binder:null,
                types:[typeof(int)],
                modifiers:null)!;
            this.scaleElementCount=this.benchmarkExecutionModeType.GetMethod(
                "ScaleElementCount",
                BindingFlags.Static|BindingFlags.NonPublic,
                binder:null,
                types:[typeof(int)],
                modifiers:null)!;
            this.captureResult=type.GetMethod(
                "CaptureBenchmarkMethodResult",
                BindingFlags.Instance|BindingFlags.Public,
                binder:null,
                types:[typeof(string),typeof(bool)],
                modifiers:null)!;
        }

        public Assembly Assembly { get; }

        public Type Type { get; }

        public static BenchmarkTarget Load(string assemblyName,string typeName) {
            var assembly=LoadAssembly(assemblyName);
            var type=assembly.GetType(typeName,throwOnError:true)!;
            return new BenchmarkTarget(assembly,type);
        }

        public void EnableComparisonMode(int elementCount) =>
            Invoke(this.enableComparisonMode,null,[elementCount]);

        public void DisableComparisonMode() =>
            Invoke(this.disableComparisonMode,null,null);

        public IDisposable UseElementCount(int elementCount,string elementScale) =>
            (IDisposable)(Invoke(this.useElementCount,null,[elementCount,elementScale])
                ??throw new InvalidOperationException("UseElementCount returned null."));

        public IDisposable UseRandomSourceSeed(int seed) =>
            (IDisposable)(Invoke(this.useRandomSourceSeed,null,[seed])
                ??throw new InvalidOperationException("UseRandomSourceSeed returned null."));

        public int ScaleElementCount(int elementCount) =>
            (int)(Invoke(this.scaleElementCount,null,[elementCount])
                ??throw new InvalidOperationException("ScaleElementCount returned null."));

        public object CreateInstance() =>
            Activator.CreateInstance(this.Type)
            ??throw new InvalidOperationException("Could not create "+this.Type.FullName+".");

        public string Capture(object instance,string methodName,bool invokeImpl) =>
            (string)(Invoke(this.captureResult,instance,[methodName,invokeImpl])
                ??throw new InvalidOperationException("Capture returned null for "+this.Type.FullName+"."+methodName+"."));

        private static Assembly LoadAssembly(string assemblyName) {
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                if(assembly.GetName().Name==assemblyName) {
                    return assembly;
                }
            }

            var assemblyPath=Path.Combine(AppContext.BaseDirectory,assemblyName+".dll");
            return File.Exists(assemblyPath)
                ? AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath)
                : Assembly.Load(new AssemblyName(assemblyName));
        }
    }
}
