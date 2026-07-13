using System.Reflection;
using BenchmarkDotNet.Attributes;

namespace UI;

public sealed record BenchmarkProject(string DisplayName,string AssemblyName,string? TypePrefix = null);

public static class BenchmarkCatalog {
    public static IReadOnlyList<BenchmarkProject> ScalableProjects { get; } = [
        new("Scalable.Persisted.Set","Benchmark.Scalable.Persisted.Collections","Benchmark.Scalable.Persisted.Collections.Set."),
        new("Scalable.Tsavorite.Set","Benchmark.Scalable.Tsavorite.Collections","Benchmark.Scalable.Tsavorite.Collections.Set."),
        new("Scalable.Persisted.Dictionary","Benchmark.Scalable.Persisted.Collections","Benchmark.Scalable.Persisted.Collections.Dictionary."),
        new("Scalable.Tsavorite.Dictionary","Benchmark.Scalable.Tsavorite.Collections","Benchmark.Scalable.Tsavorite.Collections.Dictionary."),
        new("Scalable.Persisted.List","Benchmark.Scalable.Persisted.Collections","Benchmark.Scalable.Persisted.Collections.List."),
    ];

    public static IReadOnlyList<BenchmarkProject> StandardProjects { get; } = BenchmarkSourceTabs.StandardAll
        .SelectMany(static tab => new[] {
            new BenchmarkProject(tab.LeftProject,tab.LeftAssemblyName,tab.LeftTypePrefix),
            new BenchmarkProject(tab.RightProject,tab.RightAssemblyName,tab.RightTypePrefix),
        })
        .Concat([
        new("Persisted Collection","Benchmark.Persisted.Collections"),
        new("Tsavorite Collection","Benchmark.Tsavorite.Collections"),
        ])
        .ToArray();

#if SCALABLE_BENCHMARK
    private static IReadOnlyList<BenchmarkProject> Projects => ScalableProjects;
#else
    private static IReadOnlyList<BenchmarkProject> Projects => StandardProjects;
#endif

    public static IReadOnlyList<BenchmarkCase> Load() {
        return LoadCore(static project => Assembly.Load(new AssemblyName(project.AssemblyName)),skipUnavailableAssemblies:true);
    }

    internal static IReadOnlyList<BenchmarkCase> Load(IReadOnlyDictionary<string,Assembly> registeredAssemblies) {
        ArgumentNullException.ThrowIfNull(registeredAssemblies);
        ValidateAssemblyRegistrations(Projects,registeredAssemblies.Keys);
        return LoadCore(project => registeredAssemblies.TryGetValue(project.AssemblyName,out var assembly)
            ? assembly
            : throw new InvalidOperationException("Benchmark assembly is not registered: "+project.AssemblyName),skipUnavailableAssemblies:false);
    }

    internal static void ValidateAssemblyRegistrations(
        IReadOnlyList<BenchmarkProject> projects,
        IEnumerable<string> registeredAssemblyNames) {
        var expected = projects
            .Select(static project => project.AssemblyName)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var registered = registeredAssemblyNames
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = expected.Except(registered,StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        var unexpected = registered.Except(expected,StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        if(missing.Length==0&&unexpected.Length==0) {
            return;
        }

        throw new InvalidOperationException(
            "Benchmark assembly registration mismatch. Missing: "
            +(missing.Length==0 ? "(none)" : string.Join(", ",missing))
            +"; Unexpected: "
            +(unexpected.Length==0 ? "(none)" : string.Join(", ",unexpected)));
    }

    private static IReadOnlyList<BenchmarkCase> LoadCore(Func<BenchmarkProject,Assembly> resolveAssembly,bool skipUnavailableAssemblies) {
        var cases = new List<BenchmarkCase>();
        foreach(var project in Projects.Where(static project => !string.IsNullOrWhiteSpace(project.AssemblyName))) {
            Assembly assembly;
            try {
                assembly=resolveAssembly(project);
            } catch(Exception ex) when(skipUnavailableAssemblies&&ex is FileNotFoundException or FileLoadException or BadImageFormatException) {
                System.Diagnostics.Trace.WriteLine("Benchmark catalog skipped unavailable assembly '"+project.AssemblyName+"': "+ex.Message);
                continue;
            }
            foreach(var type in assembly.GetTypes().Where(type => IsConcreteBenchmarkType(type,project.TypePrefix)).OrderBy(static type => type.FullName,StringComparer.Ordinal)) {
                var benchmarkPath=CreateBenchmarkPath(type);
                var benchmarkTypeText=CreateBenchmarkTypeText(type);
                if(string.IsNullOrWhiteSpace(benchmarkTypeText)){
                    System.Diagnostics.Trace.WriteLine(
                        "Benchmark catalog type display is empty. Project="+project.DisplayName
                        +" Assembly="+project.AssemblyName
                        +" Type="+type.FullName
                        +" BenchmarkPath="+benchmarkPath);
                }
                foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static).Where(HasBenchmarkAttribute).OrderBy(static method => method.Name,StringComparer.Ordinal)) {
                    var arguments = GetBenchmarkArguments(method).ToArray();
                    if(arguments.Length==0){
                        cases.Add(new BenchmarkCase(project.DisplayName,project.AssemblyName,type.FullName!,method.Name,benchmarkPath,benchmarkTypeText));
                        continue;
                    }

                    foreach(var argumentSet in arguments){
                        cases.Add(new BenchmarkCase(
                            project.DisplayName,
                            project.AssemblyName,
                            type.FullName!,
                            method.Name,
                            BenchmarkArgumentFormatting.CreateKey(argumentSet),
                            argumentSet,
                            benchmarkPath,
                            benchmarkTypeText));
                    }
                }
            }
        }

        return cases;
    }

    private static bool IsConcreteBenchmarkType(Type type,string? typePrefix) =>
        type is { IsAbstract: false, ContainsGenericParameters: false }
        &&(string.IsNullOrWhiteSpace(typePrefix)||type.FullName?.StartsWith(typePrefix,StringComparison.Ordinal)==true)
        &&type.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static).Any(HasBenchmarkAttribute);

    private static bool HasBenchmarkAttribute(MethodInfo method) =>
        method.GetCustomAttribute<BenchmarkAttribute>() is not null;

    internal static string CreateBenchmarkTypeText(Type type){
        for(var current=type.BaseType;current is not null;current=current.BaseType){
            if(current.IsGenericType){
                return string.Join(",",current.GetGenericArguments().Select(ScalableBenchmarkNameFormatter.FormatRuntimeType));
            }
        }
        return ScalableBenchmarkNameFormatter.CreateBenchmarkTypeText(type.FullName??string.Empty);
    }

    private static string CreateBenchmarkPath(Type type) {
        var nestedNames=new Stack<string>();
        var root=type;
        while(root.DeclaringType is not null) {
            nestedNames.Push(NormalizeChainTypeName(root.Name));
            root=root.DeclaringType;
        }
        var logicalRoot=FindLogicalBaseType(root);
        var result=FormatLogicalType(logicalRoot??root);
        return nestedNames.Count==0?result:result+"."+string.Join(".",nestedNames);
    }

    private static Type? FindLogicalBaseType(Type type) {
        for(var current=type.BaseType;current is not null;current=current.BaseType) {
            if(current.Namespace=="Seamless.Linq.Shared"
                &&(current.Name.StartsWith("Array`",StringComparison.Ordinal)
                    ||current.Name.StartsWith("IEnumerable`",StringComparison.Ordinal)
                    ||current.Name.StartsWith("List`",StringComparison.Ordinal)
                    ||current.Name.StartsWith("Persisted`",StringComparison.Ordinal)
                    ||current.Name.StartsWith("Tsavorite`",StringComparison.Ordinal)
                    ||current.Name=="Range_start_count")) {
                return current;
            }
            if(current.Namespace=="Seamless.Linq.Optimization"
                &&(current.Name.StartsWith("Array",StringComparison.Ordinal)
                    ||current.Name.StartsWith("Enumerable",StringComparison.Ordinal)
                    ||current.Name.StartsWith("List",StringComparison.Ordinal)
                    ||current.Name.StartsWith("Range",StringComparison.Ordinal))) {
                return current;
            }
        }
        return null;
    }

    private static string FormatLogicalType(Type type) {
        var name=NormalizeOptimizationTypeName(NormalizeChainTypeName(type.Name.Split('`')[0]));
        if(!type.IsGenericType) {
            return name;
        }
        return name+"<"+string.Join(",",type.GetGenericArguments().Select(FormatElementType))+">";
    }

    private static string FormatElementType(Type type)=>type==typeof(int)?"int":type.Name;
    private static string NormalizeChainTypeName(string name)=>name.EndsWith("_chain",StringComparison.Ordinal)?name[..^6]:name;
    private static string NormalizeOptimizationTypeName(string name) {
        foreach(var suffix in new[]{"Rewrite","Pipeline","Pushdown"}) {
            if(name.EndsWith(suffix,StringComparison.Ordinal)) {
                name=name[..^suffix.Length];
                break;
            }
        }
        return name switch{"Enumerable"=>"IEnumerable","Range"=>"Range_start_count",_=>name};
    }

    private static IEnumerable<IReadOnlyList<BenchmarkArgument>> GetBenchmarkArguments(MethodInfo method){
        var parameterNames = method.GetParameters().Select(static parameter => parameter.Name??string.Empty).ToArray();
        foreach(var attribute in method.CustomAttributes.Where(static attribute =>
            attribute.AttributeType.Name=="BenchArgumentsAttribute"
            ||attribute.AttributeType.Name==nameof(ArgumentsAttribute))){
            var values = attribute.ConstructorArguments
                .SelectMany(static argument => argument.ArgumentType==typeof(object[])
                    ? argument.Value is IEnumerable<CustomAttributeTypedArgument> values
                        ? values.Select(static value => value.Value)
                        : []
                    : [argument.Value])
                .ToArray();
            var names = ResolveArgumentNames(parameterNames,values.Length);
            yield return names
                .Select((name,index) => new BenchmarkArgument(
                    NormalizeArgumentName(name),
                    index<values.Length ? FormatArgumentValue(values[index]) : string.Empty))
                .ToArray();
        }
    }

    private static string[] ResolveArgumentNames(string[] parameterNames,int valueCount) {
        if(parameterNames.Length==valueCount&&!IsParamsDataOnly(parameterNames)) {
            return parameterNames;
        }

        return valueCount switch{
            3 => ["Count","Initial","Data"],
            6 => ["Count","Index","Page","Segment","Memory","Fraction"],
            _ => parameterNames,
        };
    }

    private static bool IsParamsDataOnly(string[] parameterNames) =>
        parameterNames.Length==1
        &&string.Equals(parameterNames[0],"data",StringComparison.OrdinalIgnoreCase);

    private static string NormalizeArgumentName(string name) =>
        name switch{
            "IndexSize" => "Index",
            "PageSize" => "Page",
            "SegmentSize" => "Segment",
            "MemorySize" => "Memory",
            "MutableFraction" => "Fraction",
            "InitialCapacity" => "Initial",
            "InitialDataCapacity" => "Data",
            _ => name,
        };

    private static string FormatArgumentValue(object? value) =>
        value is IFormattable formattable
            ? formattable.ToString(null,System.Globalization.CultureInfo.InvariantCulture)
            : value?.ToString()??string.Empty;
}
