using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Benchmark.Dynamic.Worker;

public static partial class GeneratedCodeText{
    public static string Executable(string source){
        var root=CSharpSyntaxTree.ParseText(source).GetRoot();
        root=root.RemoveNodes(root.DescendantNodes().OfType<AttributeListSyntax>(),SyntaxRemoveOptions.KeepExteriorTrivia)!;
        return root.ReplaceTrivia(root.DescendantTrivia().Where(trivia=>trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)||trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)||trivia.IsKind(SyntaxKind.DisabledTextTrivia)),(_,_)=>default).ToFullString();
    }
    /// <summary>
    /// 実行計画(ExecutionPlanAttribute)の行だけを除く。計画は生成本文の写しや元の式・型名を文字列で持つので、
    /// 形を文字で確かめる試験が数えると二重に数えたり消えたはずの型名を見つけたりする。
    /// 差し替えの属性やコメントは残すので、それらを確かめる試験にはこちらを使う。
    /// </summary>
    public static string WithoutExecutionPlans(string source)=>ExecutionPlanLine().Replace(source,"");

    [GeneratedRegex(@"^[ \t]*\[global::Seamless\.FLinq\.ExecutionPlanAttribute\(.*\r?\n",RegexOptions.Multiline)]
    private static partial Regex ExecutionPlanLine();
}
