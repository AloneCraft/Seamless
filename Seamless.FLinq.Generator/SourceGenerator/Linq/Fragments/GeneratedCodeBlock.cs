namespace Seamless.FLinq.Generator;

/// <summary>
/// 生成コードを意味のある順序で合成するための最小単位です。
/// raw文字列はLeafとしてのみ保持し、インデントとブロックの順序はこの型で管理します。
/// </summary>
internal sealed class GeneratedCodeBlock {
    private readonly ImmutableArray<string> parts;

    private GeneratedCodeBlock(ImmutableArray<string> parts) {
        this.parts=parts;
    }

    internal static GeneratedCodeBlock Empty { get; } = new(ImmutableArray<string>.Empty);

    internal static GeneratedCodeBlock Leaf(string text) {
        return string.IsNullOrWhiteSpace(text)
            ? Empty
            : new GeneratedCodeBlock(ImmutableArray.Create(text));
    }

    internal static GeneratedCodeBlock Line(string text) => Leaf(text);

    internal static GeneratedCodeBlock Sequence(params GeneratedCodeBlock[] blocks) {
        return new GeneratedCodeBlock(blocks
            .Where(static block => block.parts.Length>0)
            .SelectMany(static block => block.parts)
            .ToImmutableArray());
    }

    internal GeneratedCodeBlock Indent(string indent) {
        if(this.parts.Length==0||string.IsNullOrEmpty(indent)) {
            return this;
        }

        return new GeneratedCodeBlock(this.parts
            .Select(part => GeneratedTextFormatting.IndentLines(part,indent))
            .ToImmutableArray());
    }

    internal string Render() {
        return string.Join("\n",this.parts.Select(static part => part.TrimEnd('\r','\n')));
    }
}
