using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Management.Tests;

// プロジェクトの設定の印(SeamlessManagedDatabase=true)で、SDK のビルドでも Manager と同じになること(段階7)。
//  - 使用制限は DB 生成器の解析器が出す(印のあるプロジェクトだけ)。
//  - DB 生成器が回収できる型の印(DatabaseGenerator.Collectible)を足し、MemoryPack の共通の登録先へ登録しない型を作る。
// 設計：Document/refactoring-designs/20261115-Managerの設計と実装の照合.md の段階7・決定 A。
public sealed class ManagedDatabaseBuildMarker{
    private sealed class Global(bool managed):AnalyzerConfigOptions{
        public override bool TryGetValue(string key,out string value){
            if(managed&&key=="build_property.SeamlessManagedDatabase"){value="true";return true;}
            value=null!;return false;
        }
    }
    private sealed class Options(bool managed):AnalyzerConfigOptionsProvider{
        public override AnalyzerConfigOptions GlobalOptions{get;}=new Global(managed);
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)=>new Global(false);
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)=>new Global(false);
    }
    private static readonly CSharpParseOptions Parse=new(LanguageVersion.CSharp14);
    private static CSharpCompilation Compile(string source)=>CSharpCompilation.Create("Managed"+Guid.NewGuid().ToString("N"),
        [CSharpSyntaxTree.ParseText(source,Parse,"user.cs")],Seamless.Management.SourceEditor.PlatformReferences(),new(OutputKind.DynamicallyLinkedLibrary));

    [Theory,InlineData(true),InlineData(false)]
    public async Task CodePolicyAppliesOnlyToManagedDatabaseProjects(bool managed){
        var compilation=Compile("namespace UserCode{public static class Q{public static string Read()=>System.IO.File.ReadAllText(\"x\");}}");
        var analyzers=compilation.WithAnalyzers([new Seamless.Database.Generator.ManagedDatabaseCodePolicyAnalyzer()],new AnalyzerOptions([],new Options(managed)));
        var diagnostics=await analyzers.GetAnalyzerDiagnosticsAsync();
        Assert.Equal(managed,diagnostics.Any(item=>item.Id==Seamless.Database.Generator.ManagedDatabaseCodePolicyAnalyzer.DiagnosticId&&item.Severity==DiagnosticSeverity.Error));
    }

    [Theory,InlineData(true),InlineData(false)]
    public void ManagedMarkerMakesTheGeneratorBuildCollectibleRoots(bool managed){
        var compilation=Compile("using Seamless.Database;namespace UserCode{internal static partial class DatabaseGenerator;public sealed partial class Db(DatabaseContainerOptions options):DatabaseContainer(options);namespace Tables{public partial struct Item{[Key] public partial int Id{get;}public partial string Name{get;}}}}");
        GeneratorDriver driver=CSharpGeneratorDriver.Create([new Seamless.Database.Generator.DatabaseSchemaGenerator().AsSourceGenerator()],parseOptions:Parse,optionsProvider:new Options(managed));
        driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out var output,out _);
        var collectible=output.GetTypeByMetadataName("UserCode.DatabaseGenerator")?.GetMembers("Collectible").OfType<IFieldSymbol>().SingleOrDefault();
        Assert.Equal(managed,collectible?.ConstantValue is true);
        // 回収できる型は MemoryPack の共通の登録先(解除できない)へ型を登録しない。
        var generated=string.Concat(driver.GetRunResult().GeneratedTrees.Select(tree=>tree.ToString()));
        Assert.Equal(!managed,generated.Contains("MemoryPackable",StringComparison.Ordinal));
    }
}
