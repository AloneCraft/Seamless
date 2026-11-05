using Microsoft.CodeAnalysis.CSharp;
using System.Threading;
namespace Shared.Database.Generation;

internal static class DatabaseAnalysisCompilation{
    internal static Compilation Create(Compilation compilation,CancellationToken cancellationToken){
        var hasSchema=false;
        foreach(var tree in compilation.SyntaxTrees){
            if(tree.GetRoot(cancellationToken).DescendantNodes().OfType<TypeDeclarationSyntax>()
                .Any(static declaration=>declaration.Identifier.ValueText=="DatabaseGenerator")){
                hasSchema=true;
                break;
            }
        }
        if(!hasSchema){return compilation;}
        var parse=compilation.SyntaxTrees.FirstOrDefault()?.Options as CSharpParseOptions;
        GeneratorDriver driver=CSharpGeneratorDriver.Create(
            [new DatabaseSchemaGeneration().AsSourceGenerator(),new DatabaseContainerGeneration().AsSourceGenerator()],
            parseOptions:parse);
        driver.RunGeneratorsAndUpdateCompilation(compilation,out var analysis,out _,cancellationToken);
        return analysis;
    }
}
