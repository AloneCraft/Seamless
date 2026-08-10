namespace Seamless.FLinq;

// Shared formatting primitives used by internal generated-source render models.
internal static class GeneratedTextFormatting {
    internal static string IndentLines(string text,string indent) {
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