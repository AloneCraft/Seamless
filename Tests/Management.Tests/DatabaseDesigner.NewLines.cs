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
        const string body="var text=@\"a\nb\";\nreturn text.Length;";
        var design=new QueryDesign("Length","",DatabaseQueryKind.ScalarFunction,[],[],"int",body);
        var applied=Designer.ApplyQuery(definition,design);
        var database=applied.Source.Files.Single(file=>file.Path=="Database.cs").Text;
        Assert.Contains(body,database,StringComparison.Ordinal);
        // 利用者の本文を除けば、Managerが作った部分を含めてファイルの改行はCRLFだけ。
        var rest=database.Replace(body,"",StringComparison.Ordinal);
        Assert.DoesNotContain("\n",rest.Replace("\r\n","",StringComparison.Ordinal),StringComparison.Ordinal);
    }
}
