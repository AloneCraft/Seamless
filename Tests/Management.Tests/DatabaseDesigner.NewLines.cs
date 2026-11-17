using Seamless.Management;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

// Managerの改行の扱い：利用者の本文は書き換えない。Managerが作る断片は書き込み先のファイルの改行に合わせ、
// 新しいファイルはLFで作る。
public sealed partial class DatabaseDesigner{
    [Theory]
    [InlineData("",SourceNewLine.Default)]
    [InlineData("no newline",SourceNewLine.Default)]
    [InlineData("a\nb\r\nc","\n")]
    [InlineData("a\r\nb\nc","\r\n")]
    [InlineData("a\rb","\r")]
    public void SourceNewLineFollowsTheFirstNewLineOfTheFile(string text,string expected)=>
        Assert.Equal(expected,SourceNewLine.Of(text));

    [Fact]
    public void GeneratedFragmentsFollowTheFileAndTheUserBodyIsKeptAsTyped(){
        var definition=Designer.Empty("newlines","New lines");
        definition=definition with{Source=definition.Source with{Files=definition.Source.Files.Select(file=>file with{Text=file.Text.Replace("\n","\r\n",StringComparison.Ordinal)}).ToArray()}};
        // 利用者が入力した本文。逐語的文字列の中の改行(LF)は値の一部なので、そのまま残らなければならない。
        // 本体は return 文1つ(式1つ)。文字列の中の改行も、文の途中の改行も、入力どおりに残る。
        const string body="return @\"a\nb\"\n    .Length;";
        var design=new QueryDesign("Length","",DatabaseQueryKind.ScalarFunction,[],[],"int",body);
        var applied=Designer.ApplyQuery(definition,design);
        // 関数は新しいファイル(Queries/<名前>.cs)へ書く。新しいファイルは LF で作る。
        var database=applied.Source.Files.Single(file=>file.Path=="Queries/Length.cs").Text;
        Assert.Contains(body,database,StringComparison.Ordinal);
        // 利用者の本文を除けば、Managerが作った部分の改行は LF だけ(既存ファイルの CRLF には引きずられない)。
        var rest=database.Replace(body,"",StringComparison.Ordinal);
        Assert.DoesNotContain("\r",rest,StringComparison.Ordinal);
    }
}
