using System.Diagnostics;
using System.Globalization;

using Seamless.Linq;
using Seamless.Tsavorite.Collections;

using Test.System.Linq.Intercept;

var options = RunnerOptions.Parse(args);
var cases = DynamicCase.Create(options).ToArray();
var rows = new List<DynamicRow>(cases.Length);
var exitCode = 0;

Console.WriteLine("artifact=");
Console.WriteLine(DynamicRow.Header);

foreach(var testCase in cases) {
    var row = DynamicCaseRunner.Run(testCase,options.Iterations);
    rows.Add(row);
    Console.WriteLine(row.ToCsv());
    if(row.Status!="OK" && row.Status!="SKIP_VERIFY") {
        exitCode=1;
    }
}

if(rows.Count==0) {
    exitCode=1;
}

return exitCode;

internal sealed record RunnerOptions(
    bool RandomOnly,
    int RandomCount,
    int Iterations,
    int Seed,
    string[] Types,
    string[] Methods) {
    public static RunnerOptions Parse(string[] args) {
        var randomOnly = false;
        var randomCount = 10;
        var iterations = 1;
        var seed = 1729;
        var types = new[] { "int" };
        var methods = Array.Empty<string>();

        for(var index = 0; index<args.Length; index++) {
            switch(args[index]) {
                case "--random-only":
                    randomOnly=true;
                    break;
                case "--random-count":
                    if(!TryReadValue(args,ref index,out var value)) {
                        break;
                    }
                    randomCount=ParsePositiveInt(value,randomCount);
                    break;
                case "--iterations":
                    if(!TryReadValue(args,ref index,out value)) {
                        break;
                    }
                    iterations=ParsePositiveInt(value,iterations);
                    break;
                case "--seed":
                    if(!TryReadValue(args,ref index,out value)) {
                        break;
                    }
                    seed=int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out var parsedSeed) ? parsedSeed : seed;
                    break;
                case "--types":
                    if(!TryReadValue(args,ref index,out value)) {
                        break;
                    }
                    types=SplitCsv(value);
                    break;
                case "--methods":
                    if(!TryReadValue(args,ref index,out value)) {
                        break;
                    }
                    methods=SplitCsv(value);
                    break;
                case "--configuration":
                    _=TryReadValue(args,ref index,out _);
                    break;
            }
        }

        return new RunnerOptions(randomOnly,randomCount,iterations,seed,types,methods);
    }

    private static bool TryReadValue(string[] args,ref int index,out string value) {
        if(index+1>=args.Length) {
            value=string.Empty;
            return false;
        }

        index++;
        value=args[index];
        return true;
    }

    private static int ParsePositiveInt(string value,int fallback) =>
        int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out var parsed) && parsed>0
            ? parsed
            : fallback;

    private static string[] SplitCsv(string value) {
        var values = value
            .Split(',',StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static item => item.Length>0)
            .ToArray();
        return values.Length==0 ? ["int"] : values;
    }
}

internal sealed record DynamicCase(string MethodName,string TypeName,int Length,bool Ordered) {
    public static IEnumerable<DynamicCase> Create(RunnerOptions options) {
        var methodNames = options.Methods.Length>0
            ? options.Methods
            : EnumerableComparisonCatalog.BenchmarkMethodNames.ToArray();

        if(options.RandomOnly || options.Methods.Length==0) {
            var random = new Random(options.Seed);
            methodNames=methodNames
                .OrderBy(_ => random.Next())
                .Take(Math.Min(options.RandomCount,methodNames.Length))
                .ToArray();
        }

        for(var index = 0; index<methodNames.Length; index++) {
            var methodName = methodNames[index];
            yield return new DynamicCase(
                methodName,
                options.Types[index%options.Types.Length],
                4,
                IsOrdered(methodName));
        }
    }

    private static bool IsOrdered(string methodName) =>
        methodName.StartsWith("Order",StringComparison.Ordinal)
        || methodName.StartsWith("ThenBy",StringComparison.Ordinal);
}

internal static class DynamicCaseRunner {
    public static DynamicRow Run(DynamicCase testCase,int iterations) {
        try {
            var system = Measure(iterations,() => EnumerableComparisonCatalog.Normalize(EnumerableComparisonCatalog.RunSystem(testCase.MethodName)));

            using var optimizedScope = LinqCounterScope.Start();
            var optimized = Measure(iterations,() => EnumerableComparisonCatalog.Normalize(EnumerableComparisonCatalog.RunFLinq(testCase.MethodName)));
            var optimizedCounter = CounterSnapshot.From(optimizedScope.Counter);

            using var noOptimizedScope = LinqCounterScope.Start();
            var noOptimized = Measure(iterations,() => EnumerableComparisonCatalog.Normalize(EnumerableComparisonCatalog.RunFLinqNoOptimized(testCase.MethodName)));
            var noOptimizedCounter = CounterSnapshot.From(noOptimizedScope.Counter);

            var status = DetermineStatus(system.Value,optimized.Value,noOptimized.Value,optimizedCounter,noOptimizedCounter);
            return DynamicRow.From(testCase,system.ElapsedNs,optimized.ElapsedNs,noOptimized.ElapsedNs,optimizedCounter,noOptimizedCounter,status);
        } catch(Exception ex) {
            return DynamicRow.From(testCase,0,0,0,CounterSnapshot.Empty,CounterSnapshot.Empty,CleanStatus(ex.GetType().Name));
        }
    }

    private static Measurement Measure(int iterations,Func<string> action) {
        var value = string.Empty;
        var stopwatch = Stopwatch.StartNew();
        for(var index = 0; index<iterations; index++) {
            value=action();
        }

        stopwatch.Stop();
        return new Measurement(ToNanoseconds(stopwatch.ElapsedTicks),value);
    }

    private static string DetermineStatus(
        string system,
        string optimized,
        string noOptimized,
        CounterSnapshot optimizedCounter,
        CounterSnapshot noOptimizedCounter) {
        if(system!=optimized || system!=noOptimized) {
            return "FAIL_RESULT";
        }

        if(optimizedCounter.PhysicalDelegate>noOptimizedCounter.PhysicalDelegate) {
            return "FAIL_COUNTER";
        }

        if(noOptimizedCounter.LogicalDelegate!=noOptimizedCounter.PhysicalDelegate) {
            return "FAIL_COUNTER";
        }

        return "OK";
    }

    private static long ToNanoseconds(long elapsedTicks) =>
        (long)Math.Round(elapsedTicks*(1_000_000_000.0/Stopwatch.Frequency));

    private static string CleanStatus(string status) =>
        status.Replace(',','_').Replace('\r','_').Replace('\n','_');

    private sealed record Measurement(long ElapsedNs,string Value);
}

internal sealed record CounterSnapshot(
    long Physical,
    long Logical,
    long Where,
    long Select,
    long SelectManyOuter,
    long SelectManyInner,
    long JoinOuter,
    long JoinInner,
    long GroupJoinOuter,
    long GroupJoinInner,
    long LeftJoinOuter,
    long LeftJoinInner,
    long RightJoinOuter,
    long RightJoinInner,
    long HashBuild,
    long HashProbe,
    long BufferAdd,
    long TryGetNonEnumeratedCount,
    long LogicalDelegate,
    long PhysicalDelegate) {
    public static CounterSnapshot Empty { get; } = new(0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0);

    public static CounterSnapshot From(LinqCounter counter) =>
        new(
            counter.PhysicalMoveNext,
            counter.LogicalOperations,
            counter.LogicalWhere,
            counter.LogicalSelect,
            counter.LogicalSelectManyOuter,
            counter.LogicalSelectManyInner,
            counter.LogicalJoinOuter,
            counter.LogicalJoinInner,
            counter.LogicalGroupJoinOuter,
            counter.LogicalGroupJoinInner,
            counter.LogicalLeftJoinOuter,
            counter.LogicalLeftJoinInner,
            counter.LogicalRightJoinOuter,
            counter.LogicalRightJoinInner,
            counter.HashBuild,
            counter.HashProbe,
            counter.BufferAdd,
            counter.TryGetNonEnumeratedCount,
            counter.LogicalDelegate,
            counter.PhysicalDelegate);
}

internal sealed record DynamicRow(
    string Name,
    string Type,
    int Length,
    bool Ordered,
    long SystemNs,
    long FLinqNs,
    long NoOptimizedNs,
    CounterSnapshot OptimizedCounter,
    CounterSnapshot NoOptimizedCounter,
    string Status) {
    public const string Header = "name,type,length,ordered,systemNs,flinqNs,noOptimizedNs,physical,logical,where,select,selectManyOuter,selectManyInner,joinOuter,joinInner,groupJoinOuter,groupJoinInner,leftJoinOuter,leftJoinInner,rightJoinOuter,rightJoinInner,hashBuild,hashProbe,bufferAdd,tryGetNonEnumeratedCount,logicalDelegate,physicalDelegate,noOptimizedLogicalDelegate,noOptimizedPhysicalDelegate,status";

    public static DynamicRow From(
        DynamicCase testCase,
        long systemNs,
        long flinqNs,
        long noOptimizedNs,
        CounterSnapshot optimizedCounter,
        CounterSnapshot noOptimizedCounter,
        string status) =>
        new(
            testCase.MethodName,
            testCase.TypeName,
            testCase.Length,
            testCase.Ordered,
            systemNs,
            flinqNs,
            noOptimizedNs,
            optimizedCounter,
            noOptimizedCounter,
            status);

    public string ToCsv() =>
        string.Join(
            ',',
            Clean(this.Name),
            Clean(this.Type),
            this.Length.ToString(CultureInfo.InvariantCulture),
            this.Ordered.ToString(CultureInfo.InvariantCulture),
            this.SystemNs.ToString(CultureInfo.InvariantCulture),
            this.FLinqNs.ToString(CultureInfo.InvariantCulture),
            this.NoOptimizedNs.ToString(CultureInfo.InvariantCulture),
            this.OptimizedCounter.Physical.ToString(CultureInfo.InvariantCulture),
            this.OptimizedCounter.Logical.ToString(CultureInfo.InvariantCulture),
            this.OptimizedCounter.Where.ToString(CultureInfo.InvariantCulture),
            this.OptimizedCounter.Select.ToString(CultureInfo.InvariantCulture),
            this.OptimizedCounter.SelectManyOuter.ToString(CultureInfo.InvariantCulture),
            this.OptimizedCounter.SelectManyInner.ToString(CultureInfo.InvariantCulture),
            this.OptimizedCounter.JoinOuter.ToString(CultureInfo.InvariantCulture),
            this.OptimizedCounter.JoinInner.ToString(CultureInfo.InvariantCulture),
            this.OptimizedCounter.GroupJoinOuter.ToString(CultureInfo.InvariantCulture),
            this.OptimizedCounter.GroupJoinInner.ToString(CultureInfo.InvariantCulture),
            this.OptimizedCounter.LeftJoinOuter.ToString(CultureInfo.InvariantCulture),
            this.OptimizedCounter.LeftJoinInner.ToString(CultureInfo.InvariantCulture),
            this.OptimizedCounter.RightJoinOuter.ToString(CultureInfo.InvariantCulture),
            this.OptimizedCounter.RightJoinInner.ToString(CultureInfo.InvariantCulture),
            this.OptimizedCounter.HashBuild.ToString(CultureInfo.InvariantCulture),
            this.OptimizedCounter.HashProbe.ToString(CultureInfo.InvariantCulture),
            this.OptimizedCounter.BufferAdd.ToString(CultureInfo.InvariantCulture),
            this.OptimizedCounter.TryGetNonEnumeratedCount.ToString(CultureInfo.InvariantCulture),
            this.OptimizedCounter.LogicalDelegate.ToString(CultureInfo.InvariantCulture),
            this.OptimizedCounter.PhysicalDelegate.ToString(CultureInfo.InvariantCulture),
            this.NoOptimizedCounter.LogicalDelegate.ToString(CultureInfo.InvariantCulture),
            this.NoOptimizedCounter.PhysicalDelegate.ToString(CultureInfo.InvariantCulture),
            Clean(this.Status));

    private static string Clean(string value) =>
        value.Replace(',','_').Replace('\r','_').Replace('\n','_');
}
