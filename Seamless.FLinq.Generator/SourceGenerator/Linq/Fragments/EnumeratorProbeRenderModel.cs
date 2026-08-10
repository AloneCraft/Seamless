namespace Seamless.FLinq;

// Renders optional enumerator probe members after Fragment has resolved their semantic body.
// The compact default form and the inlined full form intentionally preserve the existing output shape.
internal static class EnumeratorProbeRenderModel {
    internal static string Render(string renderedBody,string defaultBody,string signature,string compactMember,string aggressiveInliningAttribute) {
        if(Normalize(renderedBody)==Normalize(defaultBody)) {
            return $"    {compactMember}";
        }

        return $$"""
                {{aggressiveInliningAttribute}}
                {{signature}} {
            {{IndentLines(renderedBody,"        ")}}
                }
            """;
    }

    private static string Normalize(string body) =>
        body.Replace("\r\n","\n").Trim();

    private static string IndentLines(string text,string indent) {
        if(string.IsNullOrWhiteSpace(text)) return string.Empty;

        var lines=text.Replace("\r\n","\n").Split('\n');
        var result=new StringBuilder();
        for(var i=0;i<lines.Length;i++) {
            var line=lines[i];
            if(!string.IsNullOrWhiteSpace(line)) result.Append(indent).Append(line);
            if(i<lines.Length-1) result.AppendLine();
        }
        return result.ToString();
    }
}