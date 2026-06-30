using System.Reflection;
using BenchmarkDotNet.Attributes;

namespace UI;

public sealed record BenchmarkProject(string DisplayName,string AssemblyName,string? TypePrefix = null);

public static class BenchmarkCatalog {
    public static IReadOnlyList<BenchmarkProject> ScalableProjects { get; } = [
        new("Scalable.Persisted.Set","Scalable.Persisted.Collections","Scalable.Persisted.Collections.Set."),
        new("Scalable.Tsavorite.Set","Scalable.Tsavorite.Collections","Scalable.Tsavorite.Collections.Set."),
        new("Scalable.Persisted.Dictionary","Scalable.Persisted.Collections","Scalable.Persisted.Collections.Dictionary."),
        new("Scalable.Tsavorite.Dictionary","Scalable.Tsavorite.Collections","Scalable.Tsavorite.Collections.Dictionary."),
        new("Scalable.Persisted.List","Scalable.Persisted.Collections","Scalable.Persisted.Collections.List."),
    ];

    public static IReadOnlyList<BenchmarkProject> StandardProjects { get; } = BenchmarkSourceTabs.StandardAll
        .SelectMany(static tab => new[] {
            new BenchmarkProject(tab.LeftProject,tab.LeftAssemblyName),
            new BenchmarkProject(tab.RightProject,tab.RightAssemblyName),
        })
        .Concat([
        new("Persisted Collection","Persisted.Collections"),
        new("Tsavorite Collection","Tsavorite.Collections"),
        ])
        .ToArray();

#if SCALABLE_BENCHMARK
    private static IReadOnlyList<BenchmarkProject> Projects => ScalableProjects;
#else
    private static IReadOnlyList<BenchmarkProject> Projects => StandardProjects;
#endif

    public static IReadOnlyList<BenchmarkCase> Load() {
        var cases = new List<BenchmarkCase>();
        foreach(var project in Projects.Where(static project => !string.IsNullOrWhiteSpace(project.AssemblyName))) {
            var assembly = Assembly.Load(new AssemblyName(project.AssemblyName));
            foreach(var type in assembly.GetTypes().Where(type => IsConcreteBenchmarkType(type,project.TypePrefix)).OrderBy(static type => type.FullName,StringComparer.Ordinal)) {
                foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static).Where(HasBenchmarkAttribute).OrderBy(static method => method.Name,StringComparer.Ordinal)) {
                    var arguments = GetBenchmarkArguments(method).ToArray();
                    if(arguments.Length==0){
                        cases.Add(new BenchmarkCase(project.DisplayName,project.AssemblyName,type.FullName!,method.Name));
                        continue;
                    }

                    foreach(var argumentSet in arguments){
                        cases.Add(new BenchmarkCase(
                            project.DisplayName,
                            project.AssemblyName,
                            type.FullName!,
                            method.Name,
                            BenchmarkArgumentFormatting.CreateKey(argumentSet),
                            argumentSet));
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

    private static IEnumerable<IReadOnlyList<BenchmarkArgument>> GetBenchmarkArguments(MethodInfo method){
        var parameterNames = method.GetParameters().Select(static parameter => parameter.Name??string.Empty).ToArray();
        foreach(var attribute in method.CustomAttributes.Where(static attribute =>
            attribute.AttributeType.Name is "BenchArgumentsAttribute" or "ArgumentsAttribute")){
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
