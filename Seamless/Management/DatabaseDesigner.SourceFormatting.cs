using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Seamless.Management;

public static partial class DatabaseDesigner{
    // ファイルの構造を使い、部分宣言だけを切り出しても名前空間やextensionの字下げを保つ。
    internal static string FormatObjectPart(SourceSnapshot source,ObjectDefinitionPart part){
        var text=source.Files.Single(file=>file.Path==part.Path).Text;
        var formatted=FormatCompactSource(text);
        if(formatted==text){return part.Text;}
        var span=new TextSpan(part.Start,part.Length);
        var root=SyntaxFactory.ParseCompilationUnit(text);
        var declaration=root.FindNode(span,getInnermostNodeForTie:true);
        if(declaration is not MemberDeclarationSyntax||declaration.FullSpan!=span){return part.Text;}
        var positions=new Stack<int>();
        for(var node=declaration;node.Parent is{} parent;node=parent){
            positions.Push(parent.ChildNodes().TakeWhile(child=>child.FullSpan!=node.FullSpan||child.RawKind!=node.RawKind).Count());
        }
        SyntaxNode result=SyntaxFactory.ParseCompilationUnit(formatted);
        foreach(var position in positions){result=result.ChildNodes().ElementAt(position);}
        return result.ToFullString();
    }

    internal static string FormatCompactSource(string text){
        var root=SyntaxFactory.ParseCompilationUnit(text);
        if(root.ContainsDiagnostics||root.ContainsDirectives){return text;}
        var normalized=root.NormalizeWhitespace(eol:"\n");
        var source=SourceText.From(normalized.ToFullString());
        var edits=new List<TextChange>();
        SyntaxToken previous=default;
        foreach(var token in normalized.DescendantTokens()){
            if(previous.RawKind!=0&&token.SpanStart>previous.Span.End){
                var span=TextSpan.FromBounds(previous.Span.End,token.SpanStart);
                var gap=source.ToString(span);
                if(gap.All(char.IsWhiteSpace)){
                    if(token.IsKind(SyntaxKind.OpenBraceToken)&&token.Parent is BaseNamespaceDeclarationSyntax or TypeDeclarationSyntax or ExtensionBlockDeclarationSyntax or BlockSyntax or AccessorListSyntax){
                        edits.Add(new(span,""));
                    }else if(gap.Contains('\n')){
                        var singleLine=gap[gap.LastIndexOf('\n')..];
                        if(singleLine!=gap){edits.Add(new(span,singleLine));}
                    }else if(CanJoin(previous,token)){
                        edits.Add(new(span,""));
                    }
                }
            }
            previous=token;
        }
        var formatted=source.WithChanges(edits).ToString().TrimEnd()+'\n';
        return SyntaxFactory.AreEquivalent(root,SyntaxFactory.ParseCompilationUnit(formatted))?formatted:text;

        static bool CanJoin(SyntaxToken left,SyntaxToken right){
            if(!SyntaxFacts.IsPunctuation(left.Kind())&&!SyntaxFacts.IsPunctuation(right.Kind())){return false;}
            if(right.IsKind(SyntaxKind.IdentifierToken)&&right.Parent is MemberDeclarationSyntax or ParameterSyntax or VariableDeclaratorSyntax){return false;}
            var tokens=SyntaxFactory.ParseTokens(left.Text+right.Text).Where(item=>!item.IsKind(SyntaxKind.EndOfFileToken)).ToArray();
            return tokens is [var first,var second]&&first.Text==left.Text&&second.Text==right.Text;
        }
    }
}
