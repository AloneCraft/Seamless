using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Benchmark.Dynamic.Worker;

/// <summary>期待値の受け渡しに使うdynamicを呼出元の静的型で置換し、DLRによる回帰アセンブリの保持を避ける。</summary>
internal static class RegressionExpectedTypeResolver{
    public static CSharpCompilation Resolve(CSharpCompilation compilation){
        var replacements=new Dictionary<SyntaxTree,Dictionary<int,ITypeSymbol>>();
        foreach(var tree in compilation.SyntaxTrees){
            var model=compilation.GetSemanticModel(tree);
            foreach(var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()){
                if(model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method){continue;}
                for(var index=0;index<method.Parameters.Length&&index<invocation.ArgumentList.Arguments.Count;index++){
                    var parameter=method.Parameters[index];
                    if(parameter.Type.TypeKind!=TypeKind.Dynamic||parameter.Name!="expected"){continue;}
                    var argument=invocation.ArgumentList.Arguments[index];
                    var type=model.GetTypeInfo(argument.Expression).Type;
                    if(type is null||type.TypeKind is TypeKind.Dynamic or TypeKind.Error){continue;}
                    if(parameter.DeclaringSyntaxReferences.SingleOrDefault()?.GetSyntax() is not ParameterSyntax declaration){continue;}
                    if(!replacements.TryGetValue(declaration.SyntaxTree,out var entries)){replacements.Add(declaration.SyntaxTree,entries=new());}
                    if(entries.TryGetValue(declaration.SpanStart,out var previous)&&!SymbolEqualityComparer.Default.Equals(previous,type)){
                        throw new InvalidDataException("期待値の型が呼出元間で一致しません: "+method.ToDisplayString());
                    }
                    entries[declaration.SpanStart]=type;
                }
            }
        }
        foreach(var (tree,entries) in replacements){
            var root=new Resolver(entries).Visit(tree.GetRoot())!;
            compilation=compilation.ReplaceSyntaxTree(tree,tree.WithRootAndOptions(root,tree.Options));
        }
        var unresolved=compilation.SyntaxTrees.SelectMany(tree=>tree.GetRoot().DescendantNodes().OfType<ParameterSyntax>())
            .Where(parameter=>parameter.Identifier.ValueText=="expected"&&parameter.Type?.ToString().Contains("dynamic",StringComparison.Ordinal)==true).ToArray();
        if(unresolved.Length!=0){throw new InvalidDataException("期待値の静的型を解決できません: "+string.Join(", ",unresolved.Select(parameter=>parameter.GetLocation().GetLineSpan().ToString())));}
        return compilation;
    }
    private sealed class Resolver(Dictionary<int,ITypeSymbol> entries):CSharpSyntaxRewriter{
        public override SyntaxNode? VisitParameter(ParameterSyntax node){
            if(entries.TryGetValue(node.SpanStart,out var type)){
                return node.WithType(SyntaxFactory.ParseTypeName(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).WithTriviaFrom(node.Type!));
            }
            return base.VisitParameter(node);
        }
    }
}
