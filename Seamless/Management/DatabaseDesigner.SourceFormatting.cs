using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Seamless.Management;

public static partial class DatabaseDesigner{
    internal static ObjectDefinitionPart[] ReadStructureClassParts(Definition definition,string? schema){
        var container=Container(Compilation(definition),definition);
        var owner=string.IsNullOrEmpty(schema)?container:Owner(container,schema);
        if(!string.IsNullOrEmpty(schema)&&!IsSchemaType(owner)){return [];}
        return owner.DeclaringSyntaxReferences.OrderBy(reference=>reference.SyntaxTree.FilePath,StringComparer.Ordinal)
            .ThenBy(reference=>reference.Span.Start).Select(reference=>{
                var node=reference.GetSyntax();
                var part=new ObjectDefinitionPart(reference.SyntaxTree.FilePath+"#"+node.FullSpan.Start,"Class",
                    reference.SyntaxTree.FilePath,node.FullSpan.Start,node.FullSpan.Length,node.ToFullString());
                return part with{Text=FormatObjectPart(definition.Source,part)};
            }).ToArray();
    }

    internal sealed record QueryEditingFrame(string Prefix,string Body,string Suffix,string Opening,string Closing,int OpenEnd,int ClosingOffset){
        internal bool PreservesFrame(string input){
            var declaration=SyntaxFactory.ParseMemberDeclaration(this.Prefix+input+this.Suffix);
            return declaration is{ContainsDiagnostics:false}&&QueryBodyTokens(declaration) is{} tokens
                &&tokens.Open.Span.End==this.OpenEnd&&tokens.Close.SpanStart==this.Prefix.Length+input.Length+this.ClosingOffset;
        }
    }

    internal static QueryEditingFrame? ReadQueryEditingFrame(SourceSnapshot source,ObjectDefinitionPart part,string display){
        if(QueryBodyTokens(SyntaxFactory.ParseMemberDeclaration(part.Text)) is not{} original
            ||QueryBodyTokens(SyntaxFactory.ParseMemberDeclaration(display)) is not{} formatted){return null;}
        var (start,end)=BodyTextBounds(part.Text,original.Open.Span.End,original.Close.SpanStart);
        var (displayStart,displayEnd)=BodyTextBounds(display,formatted.Open.Span.End,formatted.Close.SpanStart);
        var outer=ReadExtensionFrame(source,part);
        var opening=display[..displayStart].TrimEnd();
        var closing=display[displayEnd..].TrimStart('\r','\n').TrimEnd();
        if(outer is{} extension){opening=extension.Opening+"\n"+opening;closing+="\n"+extension.Closing;}
        return new(part.Text[..start],display[displayStart..displayEnd],part.Text[end..],opening,closing,
            original.Open.Span.End,original.Close.SpanStart-end);
    }

    private static (int Start,int End) BodyTextBounds(string text,int start,int end){
        var inner=text.AsSpan(start,end-start);
        var firstBreak=inner.IndexOf('\n');
        if(firstBreak>=0&&inner[..firstBreak].Trim().IsEmpty){start+=firstBreak+1;}
        var remaining=text.AsSpan(start,end-start);
        var lastBreak=remaining.LastIndexOf('\n');
        if(lastBreak>=0&&remaining[(lastBreak+1)..].Trim().IsEmpty){
            end=start+lastBreak;
            if(end>start&&text[end-1]=='\r'){end--;}
        }else if(remaining.Trim().IsEmpty){end=start;}
        return(start,end);
    }

    private static (SyntaxToken Open,SyntaxToken Close)? QueryBodyTokens(MemberDeclarationSyntax? member){
        var getter=(member as PropertyDeclarationSyntax)?.AccessorList?.Accessors.FirstOrDefault(accessor=>accessor.IsKind(SyntaxKind.GetAccessorDeclaration));
        var block=(member as MethodDeclarationSyntax)?.Body??getter?.Body;
        if(block is not null&&!block.OpenBraceToken.IsMissing&&!block.CloseBraceToken.IsMissing){return(block.OpenBraceToken,block.CloseBraceToken);}
        var (expression,semicolon)=member switch{
            MethodDeclarationSyntax method=>(method.ExpressionBody,method.SemicolonToken),
            PropertyDeclarationSyntax{ExpressionBody:{} body,SemicolonToken:var end}=>(body,end),
            _=>(getter?.ExpressionBody,getter?.SemicolonToken??default)
        };
        if(expression is null||semicolon.RawKind==0||semicolon.IsMissing){return null;}
        return(expression.ArrowToken,semicolon);
    }

    internal static (string Opening,string Closing)? ReadExtensionFrame(SourceSnapshot source,ObjectDefinitionPart part){
        var text=source.Files.Single(file=>file.Path==part.Path).Text;
        var span=new TextSpan(part.Start,part.Length);
        var root=SyntaxFactory.ParseCompilationUnit(text);
        var declaration=root.FindNode(span,getInnermostNodeForTie:true);
        if(declaration.FullSpan!=span||declaration.Parent is not ExtensionBlockDeclarationSyntax extension
            ||extension.OpenBraceToken.IsMissing||extension.CloseBraceToken.IsMissing){return null;}
        var indent=new string(' ',4*extension.Ancestors().Count(node=>node is NamespaceDeclarationSyntax or TypeDeclarationSyntax));
        return(indent+text[extension.SpanStart..extension.OpenBraceToken.SpanStart].TrimEnd()+"{",indent+extension.CloseBraceToken.Text);
    }

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
