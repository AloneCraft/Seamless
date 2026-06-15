using System.Diagnostics;
using System.Globalization;
using System.Text;

var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
var configuration = GetOption(args,"--configuration","Release");
var iterations = GetIntOption(args,"--iterations",5);
var randomCount = GetIntOption(args,"--random-count",40);
var minRandomLength = GetIntOption(args,"--min-random-length",3);
var maxRandomLength = GetIntOption(args,"--max-random-length",5);
var seed = GetIntOption(args,"--seed",12345);
var includePairs = !HasFlag(args,"--random-only");
var includeRandom = !HasFlag(args,"--pairs-only");
var keep = HasFlag(args,"--keep");
var verbose = HasFlag(args,"--verbose");
var pause = HasFlag(args,"--pause");
var typeFilter = GetOption(args,"--types","long,double,string,struct,class")
    .Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);

var profiles = Profiles().Where(profile=>typeFilter.Contains(profile.Id)).ToArray();
if(profiles.Length==0) {
    Console.Error.WriteLine("No type profiles selected.");
    return Exit(2,pause);
}

var operations = Operations();
var cases = BuildCases(profiles,operations,includePairs,includeRandom,randomCount,minRandomLength,maxRandomLength,seed);
var artifactRoot = Path.Combine(repoRoot,"sandbox","DynamicLinq.Runner","artifacts",DateTime.Now.ToString("yyyyMMdd_HHmmss",CultureInfo.InvariantCulture));
Directory.CreateDirectory(artifactRoot);

Console.WriteLine($"repo={repoRoot}");
Console.WriteLine($"artifact={artifactRoot}");
Console.WriteLine($"cases={cases.Count}, iterations={iterations}, configuration={configuration}");
Console.WriteLine("ref-struct=skipped: ref struct cannot be used as a normal IEnumerable<T> element for these generated chains.");

var flinqProject = Path.Combine(repoRoot,"src","FLinq","FLinq.csproj");
var flinqBuild = Run("dotnet",["build",flinqProject,"-c",configuration,"-v:minimal"],repoRoot);
if(flinqBuild.ExitCode!=0) {
    return Exit(flinqBuild.ExitCode,pause);
}

var analyzerPath = Path.Combine(repoRoot,"src","FLinq","bin",configuration,"net10.0","FLinq.dll");
if(!File.Exists(analyzerPath)) {
    Console.Error.WriteLine($"Analyzer not found: {analyzerPath}");
    return Exit(3,pause);
}

WriteGeneratedProject(artifactRoot,repoRoot,analyzerPath,configuration);
File.WriteAllText(Path.Combine(artifactRoot,"Program.cs"),GenerateProgram(cases,iterations,verbose),Encoding.UTF8);

var run = Run("dotnet",["run","--project",artifactRoot,"-c",configuration],repoRoot);
if(run.ExitCode!=0) {
    Console.Error.WriteLine($"Dynamic run failed: exit={run.ExitCode}");
    if(!keep) {
        Console.Error.WriteLine($"Artifacts kept for inspection: {artifactRoot}");
    }
    return Exit(run.ExitCode,pause);
}

if(!keep) {
    Console.WriteLine($"Artifacts kept: {artifactRoot}");
}

return Exit(0,pause);

static int Exit(int exitCode,bool pause) {
    if(pause && !Console.IsInputRedirected) {
        Console.WriteLine();
        Console.WriteLine($"exit={exitCode}. Press Enter to close.");
        Console.ReadLine();
    }
    return exitCode;
}

static string FindRepoRoot(string start) {
    var dir = new DirectoryInfo(start);
    while(dir is not null) {
        if(File.Exists(Path.Combine(dir.FullName,"src","FLinq","FLinq.csproj"))) {
            return dir.FullName;
        }
        dir=dir.Parent;
    }
    throw new InvalidOperationException("Could not locate repository root.");
}

static bool HasFlag(string[] args,string name) =>
    args.Any(arg=>string.Equals(arg,name,StringComparison.OrdinalIgnoreCase));

static string GetOption(string[] args,string name,string defaultValue) {
    for(var i=0;i<args.Length;i++) {
        if(string.Equals(args[i],name,StringComparison.OrdinalIgnoreCase) && i+1<args.Length) {
            return args[i+1];
        }
        if(args[i].StartsWith(name+"=",StringComparison.OrdinalIgnoreCase)) {
            return args[i][(name.Length+1)..];
        }
    }
    return defaultValue;
}

static int GetIntOption(string[] args,string name,int defaultValue) =>
    int.TryParse(GetOption(args,name,defaultValue.ToString(CultureInfo.InvariantCulture)),NumberStyles.Integer,CultureInfo.InvariantCulture,out var value)
        ? value
        : defaultValue;

static ProcessResult Run(string fileName,IEnumerable<string> arguments,string workingDirectory) {
    var startInfo = new ProcessStartInfo(fileName) {
        WorkingDirectory=workingDirectory,
        RedirectStandardOutput=true,
        RedirectStandardError=true,
        UseShellExecute=false,
    };
    foreach(var argument in arguments) {
        startInfo.ArgumentList.Add(argument);
    }
    var output = new StringBuilder();
    using var process = new Process { StartInfo = startInfo };
    process.OutputDataReceived += (_,e) => WriteProcessLine(e.Data,Console.Out);
    process.ErrorDataReceived += (_,e) => WriteProcessLine(e.Data,Console.Error);
    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    process.WaitForExit();
    return new ProcessResult(process.ExitCode,output.ToString());

    void WriteProcessLine(string? line,TextWriter writer) {
        if(line is null) {
            return;
        }
        lock(output) {
            output.AppendLine(line);
        }
        writer.WriteLine(line);
    }
}

static void WriteGeneratedProject(string directory,string repoRoot,string analyzerPath,string configuration) {
    var collectionsProject = Path.Combine(repoRoot,"src","Tsavorite.Collections","Tsavorite.Collections.csproj");
    var project = $$"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net10.0</TargetFramework>
            <LangVersion>preview</LangVersion>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
            <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
            <Features>InterceptorsPreview</Features>
            <InterceptorsPreviewNamespaces>$(InterceptorsPreviewNamespaces);Intercept</InterceptorsPreviewNamespaces>
            <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
            <CompilerGeneratedFilesOutputPath>obj/Generated</CompilerGeneratedFilesOutputPath>
            <NoWarn>$(NoWarn);CS8002</NoWarn>
          </PropertyGroup>
          <ItemGroup>
            <ProjectReference Include="{{collectionsProject}}" />
            <Analyzer Include="{{analyzerPath}}" />
          </ItemGroup>
          <ItemGroup>
            <Using Include="Tsavorite.SourceGenerator" />
          </ItemGroup>
        </Project>
        """;
    File.WriteAllText(Path.Combine(directory,"DynamicLinqCases.csproj"),project,Encoding.UTF8);
}

static IReadOnlyList<TypeProfile> Profiles() => [
    new(
        "long",
        "long",
        "SourceLong()",
        "SecondLong()",
        "x => x.ToString(System.Globalization.CultureInfo.InvariantCulture)",
        "(x % 2L) == 0L",
        "x + 1L",
        "new long[]{x, -x}",
        "global::System.Math.Abs(x) % 7L",
        "global::System.Math.Abs(y) % 7L",
        "x + y",
        "x + g.LongCount()",
        "x + y",
        "999L",
        "0L"),
    new(
        "double",
        "double",
        "SourceDouble()",
        "SecondDouble()",
        "x => x.ToString(\"R\", System.Globalization.CultureInfo.InvariantCulture)",
        "((long)x % 2L) == 0L",
        "x + 1.0",
        "new double[]{x, -x}",
        "(int)(global::System.Math.Abs(x) % 7.0)",
        "(int)(global::System.Math.Abs(y) % 7.0)",
        "x + y",
        "x + g.Count()",
        "x + y",
        "999.0",
        "0.0"),
    new(
        "string",
        "string",
        "SourceString()",
        "SecondString()",
        "x => x ?? \"<null>\"",
        "(x.Length % 2) == 0",
        "x + \"!\"",
        "new string[]{x, x + x}",
        "x.Length % 7",
        "y.Length % 7",
        "x + y",
        "x + g.Count().ToString(System.Globalization.CultureInfo.InvariantCulture)",
        "(x ?? string.Empty) + (y ?? string.Empty)",
        "\"zz\"",
        "string.Empty"),
    new(
        "struct",
        "SmallStruct",
        "SourceStruct()",
        "SecondStruct()",
        "x => x.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)",
        "(x.Value % 2L) == 0L",
        "new SmallStruct(x.Value + 1L)",
        "new SmallStruct[]{x, new SmallStruct(-x.Value)}",
        "global::System.Math.Abs(x.Value) % 7L",
        "global::System.Math.Abs(y.Value) % 7L",
        "new SmallStruct(x.Value + y.Value)",
        "new SmallStruct(x.Value + g.LongCount())",
        "new SmallStruct(x.Value + y.Value)",
        "new SmallStruct(999L)",
        "default"),
    new(
        "class",
        "SmallClass",
        "SourceClass()",
        "SecondClass()",
        "x => x.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)",
        "(x.Value % 2L) == 0L",
        "new SmallClass(x.Value + 1L)",
        "new SmallClass[]{x, new SmallClass(-x.Value)}",
        "global::System.Math.Abs(x.Value) % 7L",
        "global::System.Math.Abs(y.Value) % 7L",
        "new SmallClass(x.Value + y.Value)",
        "new SmallClass(x.Value + g.LongCount())",
        "new SmallClass((x is null ? 0L : x.Value) + (y is null ? 0L : y.Value))",
        "new SmallClass(999L)",
        "new SmallClass(0L)")
];

static IReadOnlyList<LinqOp> Operations() => [
    new("Where",(q,p)=>$"{q}.Where(static x => {p.WherePredicate})"),
    new("Select",(q,p)=>$"{q}.Select(static x => {p.SelectExpression})"),
    new("SelectMany",(q,p)=>$"{q}.SelectMany(static x => {p.SelectManyExpression})"),
    new("Join",(q,p)=>$"{q}.Join({p.SecondExpression}, static x => {p.OuterKeyExpression}, static y => {p.InnerKeyExpression}, static (x,y) => {p.JoinResultExpression})"),
    new("GroupJoin",(q,p)=>$"{q}.GroupJoin({p.SecondExpression}, static x => {p.OuterKeyExpression}, static y => {p.InnerKeyExpression}, static (x,g) => {p.GroupJoinResultExpression})"),
    new("LeftJoin",(q,p)=>$"{q}.LeftJoin({p.SecondExpression}, static x => {p.OuterKeyExpression}, static y => {p.InnerKeyExpression}, static (x,y) => {p.LeftRightJoinResultExpression})"),
    new("RightJoin",(q,p)=>$"{q}.RightJoin({p.SecondExpression}, static x => {p.OuterKeyExpression}, static y => {p.InnerKeyExpression}, static (x,y) => {p.LeftRightJoinResultExpression})"),
    new("Take",(q,_)=>$"{q}.Take(16)"),
    new("Skip",(q,_)=>$"{q}.Skip(1)"),
    new("OrderBy",(q,p)=>$"{q}.OrderBy(static x => {p.OuterKeyExpression})"),
    new("Reverse",(q,_)=>$"{q}.Reverse()"),
    new("Distinct",(q,_)=>$"{q}.Distinct()"),
    new("DefaultIfEmpty",(q,p)=>$"{q}.DefaultIfEmpty({p.DefaultExpression})"),
    new("Append",(q,p)=>$"{q}.Append({p.AppendExpression})"),
    new("Prepend",(q,p)=>$"{q}.Prepend({p.AppendExpression})"),
    new("Shuffle",(q,_)=>$"{q}.Shuffle()",OrderSensitive:false)
];

static List<GeneratedCase> BuildCases(
    IReadOnlyList<TypeProfile> profiles,
    IReadOnlyList<LinqOp> operations,
    bool includePairs,
    bool includeRandom,
    int randomCount,
    int minRandomLength,
    int maxRandomLength,
    int seed) {
    var cases = new List<GeneratedCase>();
    if(includePairs) {
        foreach(var profile in profiles) {
            foreach(var first in operations) {
                foreach(var second in operations) {
                    cases.Add(new GeneratedCase(profile,[first,second]));
                }
            }
        }
    }
    if(includeRandom && randomCount>0) {
        var random = new Random(seed);
        foreach(var profile in profiles) {
            for(var i=0;i<randomCount;i++) {
                var length = random.Next(minRandomLength,maxRandomLength+1);
                var chain = Enumerable.Range(0,length)
                    .Select(_=>operations[random.Next(operations.Count)])
                    .ToArray();
                cases.Add(new GeneratedCase(profile,chain));
            }
        }
    }
    return cases;
}

static string GenerateProgram(IReadOnlyList<GeneratedCase> cases,int iterations,bool verbose) {
    var sb = new StringBuilder();
    sb.AppendLine("""
        using System;
        using System.Collections.Generic;
        using System.Diagnostics;
        using System.Globalization;
        using System.Linq;
        using Tsavorite;
        using Tsavorite.SourceGenerator;

        public readonly record struct SmallStruct(long Value);
        public sealed record SmallClass(long Value);

        internal sealed class Program {
            private const int Iterations =
        """);
    sb.AppendLine($"                {iterations};");
    sb.AppendLine($"            private const bool Verbose = {(verbose ? "true" : "false")};");
    sb.AppendLine("""

            private static int Main() {
                var failed = 0;
                var total = 0;
                Console.WriteLine("name,type,length,ordered,system_ns,flinq_ns,nooptimized_ns,physical,logical,where,select,selectManyOuter,selectManyInner,joinOuter,joinInner,groupJoinOuter,groupJoinInner,leftJoinOuter,leftJoinInner,rightJoinOuter,rightJoinInner,hashBuild,hashProbe,bufferAdd,tryGetNonEnumeratedCount,logicalDelegate,physicalDelegate,nooptimizedLogicalDelegate,nooptimizedPhysicalDelegate,status");
        """);
    for(var i=0;i<cases.Count;i++) {
        var generatedCase = cases[i];
        var methodSuffix = i.ToString("D5",CultureInfo.InvariantCulture);
        var ordered = generatedCase.OrderSensitive ? "true" : "false";
        var verifiable = generatedCase.Verifiable ? "true" : "false";
        sb.AppendLine($"        failed += RunCase(\"{generatedCase.Name}\",\"{generatedCase.Profile.Id}\",{generatedCase.Chain.Count},{ordered},{verifiable},static () => System_{methodSuffix}(),static () => FLinq_{methodSuffix}(),static () => NoOptimized_{methodSuffix}(),{generatedCase.Profile.KeySelector});");
        sb.AppendLine("        total++;");
    }
    sb.AppendLine("""
                Console.Error.WriteLine($"summary: total={total}, failed={failed}");
                return failed==0 ? 0 : 1;
            }

            private static int RunCase<T>(string name,string typeName,int length,bool ordered,bool verifiable,Func<T[]> systemFunc,Func<T[]> flinqFunc,Func<T[]> noOptimizedFunc,Func<T,string> keySelector) {
                try {
                    var expected = systemFunc();
                    using var scope = LinqCounterScope.Start();
                    var actual = flinqFunc();
                    using var noOptimizedScope = LinqCounterScope.Start();
                    var noOptimizedActual = noOptimizedFunc();
                    var equivalent = !verifiable || (ordered
                        ? expected.SequenceEqual(actual)
                        : expected.Select(keySelector).Order(StringComparer.Ordinal).SequenceEqual(actual.Select(keySelector).Order(StringComparer.Ordinal)));
                    var noOptimizedEquivalent = !verifiable || (ordered
                        ? expected.SequenceEqual(noOptimizedActual)
                        : expected.Select(keySelector).Order(StringComparer.Ordinal).SequenceEqual(noOptimizedActual.Select(keySelector).Order(StringComparer.Ordinal)));
                    if(verifiable && (!equivalent || !noOptimizedEquivalent)) {
                        Console.WriteLine($"{name},{typeName},{length},{ordered},0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,FAIL");
                        Console.Error.WriteLine($"mismatch: {name} type={typeName} expected=[{string.Join(";",expected.Select(keySelector))}] actual=[{string.Join(";",actual.Select(keySelector))}]");
                        if(!noOptimizedEquivalent) {
                            Console.Error.WriteLine($"mismatch(no-optimized): {name} type={typeName} expected=[{string.Join(";",expected.Select(keySelector))}] actual=[{string.Join(";",noOptimizedActual.Select(keySelector))}]");
                        }
                        return 1;
                    }

                    var systemTicks = Measure(systemFunc);
                    var flinqTicks = MeasureWithCounter(flinqFunc,out var counter);
                    var noOptimizedTicks = MeasureWithCounter(noOptimizedFunc,out var noOptimizedCounter);
                    var systemNs = TicksToNanoseconds(systemTicks);
                    var flinqNs = TicksToNanoseconds(flinqTicks);
                    var noOptimizedNs = TicksToNanoseconds(noOptimizedTicks);
                    var status = verifiable ? "OK" : "SKIP_VERIFY";
                    Console.WriteLine($"{name},{typeName},{length},{ordered},{systemNs},{flinqNs},{noOptimizedNs},{counter.PhysicalMoveNext},{counter.LogicalOperations},{counter.LogicalWhere},{counter.LogicalSelect},{counter.LogicalSelectManyOuter},{counter.LogicalSelectManyInner},{counter.LogicalJoinOuter},{counter.LogicalJoinInner},{counter.LogicalGroupJoinOuter},{counter.LogicalGroupJoinInner},{counter.LogicalLeftJoinOuter},{counter.LogicalLeftJoinInner},{counter.LogicalRightJoinOuter},{counter.LogicalRightJoinInner},{counter.HashBuild},{counter.HashProbe},{counter.BufferAdd},{counter.TryGetNonEnumeratedCount},{counter.LogicalDelegate},{counter.PhysicalDelegate},{noOptimizedCounter.LogicalDelegate},{noOptimizedCounter.PhysicalDelegate},{status}");
                    return 0;
                } catch(Exception ex) {
                    Console.WriteLine($"{name},{typeName},{length},{ordered},0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,ERROR");
                    Console.Error.WriteLine($"error: {name} type={typeName}: {ex.GetType().Name}: {ex.Message}");
                    return 1;
                }
            }

            private static long Measure<T>(Func<T[]> func) {
                func();
                var sw = Stopwatch.StartNew();
                for(var i=0;i<Iterations;i++) {
                    GC.KeepAlive(func());
                }
                sw.Stop();
                return sw.ElapsedTicks;
            }

            private static long MeasureWithCounter<T>(Func<T[]> func,out LinqCounter counter) {
                counter = new LinqCounter();
                using(var scope = LinqCounterScope.Start(counter)) {
                    func();
                }
                var sw = Stopwatch.StartNew();
                for(var i=0;i<Iterations;i++) {
                    using var scope = LinqCounterScope.Start(counter);
                    GC.KeepAlive(func());
                }
                sw.Stop();
                return sw.ElapsedTicks;
            }

            private static long TicksToNanoseconds(long ticks) =>
                (long)(ticks * (1_000_000_000.0 / Stopwatch.Frequency) / Math.Max(1,Iterations));

            private static long[] SourceLong() => new long[]{-4,-3,-2,-1,0,1,2,3,4,5,6,7,8,9,10,10,11,12,13,14,15,16,17,18};
            private static long[] SecondLong() => new long[]{0,1,2,3,4,5,6,7,8,9,10,11,12,13};
            private static double[] SourceDouble() => new double[]{-4,-3,-2,-1,0,1,2,3,4,5,6,7,8,9,10,10,11,12,13,14,15,16,17,18};
            private static double[] SecondDouble() => new double[]{0,1,2,3,4,5,6,7,8,9,10,11,12,13};
            private static string[] SourceString() => new string[]{"a","bb","ccc","dddd","ee","fff","gggg","hh","iii","jjjj","kk","lll"};
            private static string[] SecondString() => new string[]{"x","yy","zzz","wwww","qq","rrr","ssss"};
            private static SmallStruct[] SourceStruct() => SourceLong().Select(static value => new SmallStruct(value)).ToArray();
            private static SmallStruct[] SecondStruct() => SecondLong().Select(static value => new SmallStruct(value)).ToArray();
            private static SmallClass[] SourceClass() => SourceLong().Select(static value => new SmallClass(value)).ToArray();
            private static SmallClass[] SecondClass() => SecondLong().Select(static value => new SmallClass(value)).ToArray();
        """);

    for(var i=0;i<cases.Count;i++) {
        var generatedCase = cases[i];
        var suffix = i.ToString("D5",CultureInfo.InvariantCulture);
        var chain = RenderChain(generatedCase);
        sb.AppendLine();
        sb.AppendLine("        [LinqIntercept(false)]");
        sb.AppendLine($"        private static {generatedCase.Profile.TypeName}[] System_{suffix}() => {chain}.ToArray();");
        sb.AppendLine();
        sb.AppendLine("        [LinqIntercept(true,LinqInstrumentation.Counter)]");
        sb.AppendLine($"        private static {generatedCase.Profile.TypeName}[] FLinq_{suffix}() => {chain}.ToArray();");
        sb.AppendLine();
        sb.AppendLine("        [LinqIntercept(false,LinqInstrumentation.Counter)]");
        sb.AppendLine($"        private static {generatedCase.Profile.TypeName}[] NoOptimized_{suffix}() => {chain}.ToArray();");
    }
    sb.AppendLine("}");
    return sb.ToString();
}

static string RenderChain(GeneratedCase generatedCase) {
    var current = generatedCase.Profile.SourceExpression;
    foreach(var op in generatedCase.Chain) {
        current=op.Apply(current,generatedCase.Profile);
    }
    return current;
}

internal sealed record ProcessResult(int ExitCode,string Output);

internal sealed record TypeProfile(
    string Id,
    string TypeName,
    string SourceExpression,
    string SecondExpression,
    string KeySelector,
    string WherePredicate,
    string SelectExpression,
    string SelectManyExpression,
    string OuterKeyExpression,
    string InnerKeyExpression,
    string JoinResultExpression,
    string GroupJoinResultExpression,
    string LeftRightJoinResultExpression,
    string AppendExpression,
    string DefaultExpression);

internal sealed record LinqOp(string Name,Func<string,TypeProfile,string> Apply,bool OrderSensitive = true);

internal sealed record GeneratedCase(TypeProfile Profile,IReadOnlyList<LinqOp> Chain) {
    public string Name => string.Join("_",this.Chain.Select(static op=>op.Name));
    public bool OrderSensitive => this.Chain.All(static op=>op.OrderSensitive);
    public bool Verifiable {
        get {
            var sawShuffle=false;
            foreach(var op in this.Chain) {
                if(op.Name=="Shuffle") {
                    sawShuffle=true;
                    continue;
                }
                if(sawShuffle && op.Name is "Take" or "Skip") {
                    return false;
                }
            }
            return true;
        }
    }
}
