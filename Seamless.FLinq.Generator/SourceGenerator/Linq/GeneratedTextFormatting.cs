namespace Seamless.FLinq.Generator;

// Shared formatting primitives used by internal generated-source render models.
internal static class GeneratedTextFormatting {
    internal static string IndentLines(string text,string indent) {
        if(string.IsNullOrWhiteSpace(text)) return string.Empty;
        if(TryIndentPreservingTokens(text,indent,out var preserved)) return preserved;

        var lines=text.Replace("\r\n","\n").Split('\n');
        var result=new StringBuilder();
        for(var i=0;i<lines.Length;i++) {
            var line=lines[i];
            if(!string.IsNullOrWhiteSpace(line)) result.Append(indent).Append(line);
            if(i<lines.Length-1) result.AppendLine();
        }
        return result.ToString();
    }

    // 多行token内の文字/改行を保ち、token外の行頭だけを整形する。
    internal static bool TryIndentPreservingTokens(string text,string indent,out string result) {
        result=string.Empty;
        if(text.IndexOf('\n')<0&&text.IndexOf('\r')<0) return false;
        if(text.IndexOf("@\"",StringComparison.Ordinal)<0&&text.IndexOf("$\"",StringComparison.Ordinal)<0
            &&text.IndexOf("\"\"\"",StringComparison.Ordinal)<0&&text.IndexOf("/*",StringComparison.Ordinal)<0) return false;

        var protectedSpans=new List<(int Start,int End)>();
        foreach(var token in Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseTokens(text,options:MultilineTokenParseOptions)) {
            foreach(var trivia in token.LeadingTrivia) ProtectTrivia(trivia);
            Protect(token.SpanStart,token.Span.Length);
            foreach(var trivia in token.TrailingTrivia) ProtectTrivia(trivia);
        }
        if(protectedSpans.Count==0) return false;

        var builder=new StringBuilder(text.Length);
        var spanIndex=0;
        var lineStart=0;
        while(lineStart<text.Length) {
            var lineEnd=lineStart;
            while(lineEnd<text.Length&&text[lineEnd]!='\r'&&text[lineEnd]!='\n') lineEnd++;
            while(spanIndex<protectedSpans.Count&&protectedSpans[spanIndex].End<=lineStart) spanIndex++;
            var inToken=spanIndex<protectedSpans.Count
                &&protectedSpans[spanIndex].Start<lineStart
                &&lineStart<protectedSpans[spanIndex].End;
            if(!inToken&&lineEnd>lineStart) builder.Append(indent);
            builder.Append(text,lineStart,lineEnd-lineStart);
            if(lineEnd<text.Length) {
                builder.Append(text[lineEnd++]);
                if(text[lineEnd-1]=='\r'&&lineEnd<text.Length&&text[lineEnd]=='\n') builder.Append(text[lineEnd++]);
            }
            lineStart=lineEnd;
        }
        result=builder.ToString();
        return true;

        void Protect(int start,int length) {
            if(length>0&&(text.IndexOf('\n',start,length)>=0||text.IndexOf('\r',start,length)>=0)) {
                protectedSpans.Add((start,start+length));
            }
        }
        void ProtectTrivia(SyntaxTrivia trivia) {
            if(trivia.RawKind!=(int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.EndOfLineTrivia
                &&trivia.RawKind!=(int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.WhitespaceTrivia) {
                Protect(trivia.SpanStart,trivia.Span.Length);
            }
        }
    }

    private static readonly Microsoft.CodeAnalysis.CSharp.CSharpParseOptions MultilineTokenParseOptions=
        new(Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview);
}
