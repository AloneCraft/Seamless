namespace Seamless.FLinq.Linq.Fragments;

internal sealed class GeneratedCodeWriter {
    private readonly StringBuilder builder = new();
    private int indentLevel;

    public void AppendLine(string line = "") {
        if(line.Length>0 && this.indentLevel>0) {
            this.builder.Append(new string(' ',this.indentLevel*4));
        }
        this.builder.AppendLine(line);
    }

    public IDisposable Indent() {
        this.indentLevel++;
        return new IndentScope(this);
    }

    public override string ToString() => this.builder.ToString();

    public static void AppendMember(StringBuilder builder,string source) {
        if(string.IsNullOrWhiteSpace(source)) {
            return;
        }
        if(builder.Length>0) {
            builder.AppendLine();
        }
        builder.AppendLine(source.TrimEnd());
    }

    public static void AppendIndented(StringBuilder builder,string source,string indent) {
        if(string.IsNullOrWhiteSpace(source)) {
            return;
        }

        var text = source.Replace("\r\n","\n").TrimEnd('\r','\n');
        foreach(var line in text.Split('\n')) {
            if(line.Length==0) {
                builder.AppendLine();
            } else {
                builder.Append(indent).AppendLine(line);
            }
        }
    }

    private sealed class IndentScope(GeneratedCodeWriter writer):IDisposable {
        public void Dispose() {
            writer.indentLevel--;
        }
    }
}
