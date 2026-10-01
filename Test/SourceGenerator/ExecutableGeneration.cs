using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Seamless.FLinq;
using Seamless.FLinq.Generator;

namespace Test.SourceGenerator;

/// <summary>
/// FLinq の生成コードを含めてコンパイルし、実行して意味を確かめるテストの共通部分。
/// FLinq は [LinqIntercept(true)] のメソッドの呼び出し箇所を本体展開版へ差し替えるので、
/// 試すコードは公開の入口メソッドから属性付きメソッドを呼ぶ形にする(反射で直接呼ぶと差し替わらない)。
/// </summary>
[LinqIntercept(false)]
internal static class ExecutableGeneration {
    public static (CSharpCompilation Compilation,string Generated) Generate(string source,string assemblyName,int minimumInterceptions) {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview)
            .WithFeatures([
                new("InterceptorsPreview","true"),
                new("InterceptorsNamespaces","Intercept"),
                new("InterceptorsPreviewNamespaces","Intercept"),
            ]);
        var compilation = GeneratorTestHelpers.CreateCompilation([("Container.cs",source)])
            .WithAssemblyName(assemblyName);
        var originalTree = compilation.SyntaxTrees.Single();
        var executableTree = CSharpSyntaxTree.ParseText(source,parseOptions,path: "Container.cs");
        compilation=compilation.ReplaceSyntaxTree(originalTree,executableTree);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new FLinqIncrementalGenerator().AsSourceGenerator()],
            parseOptions: parseOptions,
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None,trackIncrementalGeneratorSteps: false));
        driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out var outputCompilation,out var generatorDiagnostics);
        var errors = generatorDiagnostics
            .Concat(outputCompilation.GetDiagnostics())
            .Where(static diagnostic => diagnostic.Severity==DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(errors);
        // static class の中や反射で直接呼んだメソッドは差し替わらず、テストが空振りする。差し替えが生成されたことを確かめる。
        var generated = string.Concat(outputCompilation.SyntaxTrees.Skip(1).Select(static tree => tree.ToString()));
        Assert.True(generated.Split("InterceptsLocationAttribute(1,").Length-1>=minimumInterceptions,"LINQ の呼び出しが差し替えられていません。");
        return ((CSharpCompilation)outputCompilation,generated);
    }

    /// <summary>コンパイル結果を読み込み、型を渡して action を実行する。読み込み先はテストごとに捨てる。</summary>
    public static T Run<T>(CSharpCompilation compilation,string typeName,Func<Type,T> action) {
        using var pe = new MemoryStream();
        var emit = compilation.Emit(pe);
        Assert.True(emit.Success,string.Join(Environment.NewLine,emit.Diagnostics));
        pe.Position=0;
        var loadContext = new global::System.Runtime.Loader.AssemblyLoadContext(compilation.AssemblyName,isCollectible: true);
        try {
            return action(loadContext.LoadFromStream(pe).GetType(typeName)!);
        } finally {
            loadContext.Unload();
        }
    }
}
