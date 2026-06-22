using System.Reflection;
using System.Runtime.Loader;
using System.Text;

namespace Test.System.Linq.BenchmarkValueComparison;

public sealed class BenchmarkValueComparisonTests {
    private const int ComparisonElementCount = 1_000;
    private const string BenchmarkExecutionModeTypeName = "Benchmark.Shared.BenchmarkExecutionMode";
    private const string Methods1TypeName = "Shared.Linq.Enumerable.Methods1";

    [Fact]
    public void Methods1_results_match() {
        var linq=BenchmarkTarget.Load("Test.System.Linq.Enumerable.Linq","Test.System.Linq.Enumerable.Linq");
        var flinq=BenchmarkTarget.Load("Test.System.Linq.Enumerable.FLinq","Test.System.Linq.Enumerable.FLinq");
        var zlinq=BenchmarkTarget.Load("Test.System.Linq.Enumerable.ZLinq","Test.System.Linq.Enumerable.ZLinq");
        var targets=new[] { linq,flinq,zlinq };

        try {
            foreach(var target in targets) {
                target.EnableComparisonMode(ComparisonElementCount);
            }

            var methodNames=DiscoverMethodNames(linq.Type);
            var failures=new List<string>();
            var linqInstance=linq.CreateInstance();
            var flinqInstance=flinq.CreateInstance();
            var zlinqInstance=zlinq.CreateInstance();

            foreach(var methodName in methodNames) {
                try {
                    var expected=linq.Capture(linqInstance,methodName,invokeImpl:false);
                    AddFailureIfDifferent(failures,methodName,"LINQ private Impl",expected,linq.Capture(linqInstance,methodName,invokeImpl:true));
                    AddFailureIfDifferent(failures,methodName,"FLinq public",expected,flinq.Capture(flinqInstance,methodName,invokeImpl:false));
                    AddFailureIfDifferent(failures,methodName,"FLinq private Impl",expected,flinq.Capture(flinqInstance,methodName,invokeImpl:true));
                    AddFailureIfDifferent(failures,methodName,"ZLinq public",expected,zlinq.Capture(zlinqInstance,methodName,invokeImpl:false));
                } catch(Exception ex) {
                    failures.Add(methodName+": "+ex.GetType().FullName+": "+ex.Message);
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

    private static List<string> DiscoverMethodNames(Type type) {
        var names=new List<string>();
        foreach(var method in type.GetMethods(BindingFlags.Instance|BindingFlags.Public)) {
            if(method.DeclaringType?.FullName!=Methods1TypeName) {
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
        string expected,
        string actual) {
        if(StringComparer.Ordinal.Equals(expected,actual)) {
            return;
        }

        failures.Add(methodName+" / "+scenario
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
