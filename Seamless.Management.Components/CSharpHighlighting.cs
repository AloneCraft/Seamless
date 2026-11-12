using Microsoft.CodeAnalysis.CSharp;

namespace Seamless.Management.Components;

internal static class CSharpHighlighting{
    internal sealed record Segment(string Text,string? Style);
    internal static Segment[] Parse(string text){
        var root=CSharpSyntaxTree.ParseText(text,new CSharpParseOptions(LanguageVersion.CSharp14)).GetRoot();
        var spans=new List<(int Start,int Length,string Style)>();
        foreach(var token in root.DescendantTokens()){
            string? style=null;
            var kind=token.Kind();
            if(SyntaxFacts.IsKeywordKind(kind)){style="keyword";}
            else if(kind==SyntaxKind.NumericLiteralToken){style="number";}
            else if(kind==SyntaxKind.CharacterLiteralToken||kind.ToString().Contains("String",StringComparison.Ordinal)){style="string";}
            if(style is not null&&token.Span.Length>0){spans.Add((token.Span.Start,token.Span.Length,style));}
        }
        foreach(var trivia in root.DescendantTrivia()){
            var kind=trivia.Kind();
            if(kind is SyntaxKind.SingleLineCommentTrivia or SyntaxKind.MultiLineCommentTrivia or SyntaxKind.SingleLineDocumentationCommentTrivia or SyntaxKind.MultiLineDocumentationCommentTrivia or SyntaxKind.DisabledTextTrivia){
                spans.Add((trivia.Span.Start,trivia.Span.Length,"comment"));
            }else if(trivia.IsDirective){spans.Add((trivia.Span.Start,trivia.Span.Length,"directive"));}
        }
        var result=new List<Segment>();var position=0;
        foreach(var span in spans.OrderBy(span=>span.Start)){
            if(span.Start<position){continue;}
            if(span.Start>position){result.Add(new(text[position..span.Start],null));}
            result.Add(new(text.Substring(span.Start,span.Length),span.Style));
            position=span.Start+span.Length;
        }
        if(position<text.Length){result.Add(new(text[position..],null));}
        return result.ToArray();
    }
}
