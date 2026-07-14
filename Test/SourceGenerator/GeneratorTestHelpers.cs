using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Seamless.FLinq;

using System.Collections.Immutable;

namespace Test.SourceGenerator;

internal static class GeneratorTestHelpers {
    public static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Preview);

    public static CSharpGeneratorDriver CreateDriver() {
        return CSharpGeneratorDriver.Create(
            generators: [new FLinqIncrementalGenerator().AsSourceGenerator()],
            parseOptions: ParseOptions,
            driverOptions: new GeneratorDriverOptions(
                disabledOutputs: IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));
    }

    public static CSharpCompilation CreateCompilation(IEnumerable<(string fileName,string source)> sources) {
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithNullableContextOptions(NullableContextOptions.Enable);
        var references = GetNetCoreReferences();
        var syntaxTrees = sources
            .Select(s => CSharpSyntaxTree.ParseText(s.source,ParseOptions,path: s.fileName))
            .ToArray();

        return CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: syntaxTrees,
            references: references,
            options: options);
    }

    private static ImmutableArray<MetadataReference> GetNetCoreReferences() {
        var trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string??string.Empty;
        var refs = trustedAssemblies
            .Split(Path.PathSeparator)
            .Where(p => !string.IsNullOrWhiteSpace(p)&&File.Exists(p))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(global::Seamless.Linq.GeneratedCodeHelpers).Assembly.Location))
            .ToImmutableArray();
        return refs;
    }
}
