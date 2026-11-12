using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Benchmark.Dynamic.Worker;

public static class GeneratedCodeText{
    public static string Executable(string source){
        var root=CSharpSyntaxTree.ParseText(source).GetRoot();
        root=root.RemoveNodes(root.DescendantNodes().OfType<AttributeListSyntax>(),SyntaxRemoveOptions.KeepExteriorTrivia)!;
        return root.ReplaceTrivia(root.DescendantTrivia().Where(trivia=>trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)||trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)||trivia.IsKind(SyntaxKind.DisabledTextTrivia)),(_,_)=>default).ToFullString();
    }
}
