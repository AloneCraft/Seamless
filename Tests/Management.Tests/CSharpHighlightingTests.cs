using Seamless.Management.Components;

namespace Management.Tests;

public sealed class CSharpHighlightingTests{
    [Fact]public void HighlightingPreservesTheExactSourceAndClassifiesLiteralsAndComments(){
        const string source="// public int comment\r\npublic class Sample { string Text = \"<script>日本語😀</script>\"; decimal Value = 12.50m; }\r\n";
        var segments=CSharpHighlighting.Parse(source);
        Assert.Equal(source,string.Concat(segments.Select(segment=>segment.Text)));
        Assert.Contains(segments,segment=>segment.Style=="keyword"&&segment.Text=="public");
        Assert.Contains(segments,segment=>segment.Style=="comment"&&segment.Text.Contains("public int comment",StringComparison.Ordinal));
        Assert.Contains(segments,segment=>segment.Style=="string"&&segment.Text.Contains("<script>日本語😀</script>",StringComparison.Ordinal));
        Assert.Contains(segments,segment=>segment.Style=="number"&&segment.Text=="12.50m");
    }
    [Theory]
    [InlineData("")]
    [InlineData("var text = $\"value={1 + 2}\";")]
    [InlineData("var text = \"\"\"raw <text>\"\"\";")]
    [InlineData("#if NEVER\ninvalid code\n#endif\nclass C { string s = \"unfinished")]
    [InlineData("/// <summary>text</summary>\npartial struct Sample;")]
    public void IncompleteAndStructuredSyntaxKeepsEveryCharacter(string source){
        Assert.Equal(source,string.Concat(CSharpHighlighting.Parse(source).Select(segment=>segment.Text)));
    }
}
