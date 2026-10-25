using Benchmark.Dynamic.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Benchmark.Dynamic.Worker;

/// <summary>
/// 評価回数の検査用に、生成したクエリのソースの式本体のラムダを、<see cref="EvaluationProbe.Hit{T}"/> で包む。
/// <c>x=>x%3==0</c> は <c>x=>global::Benchmark.Dynamic.Core.EvaluationProbe.Hit(0,x%3==0)</c> になる。
/// 番号はメソッドごとに 0 から、ラムダの出現順(内側のラムダが先)に振る。各経路のメソッドは同じ式を持つので、同じ番号は同じラムダ。
/// ブロック本体のラムダは対象にしない(生成するクエリには無い)。型を決められない本体(<c>default</c>・<c>null</c> など)は、
/// コンパイルできなくなるので、呼び出し側が「検査できなかった」として扱う。
/// </summary>
public static class EvaluationProbeRewriter {
    public static string Instrument(string sourceText) {
        var tree = CSharpSyntaxTree.ParseText(sourceText,new CSharpParseOptions(LanguageVersion.Latest));
        var rewritten = new Rewriter().Visit(tree.GetRoot());
        return rewritten.ToFullString();
    }

    private sealed class Rewriter:CSharpSyntaxRewriter {
        private int next;

        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node) {
            this.next=0;
            return base.VisitMethodDeclaration(node);
        }

        public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node) {
            var visited = (SimpleLambdaExpressionSyntax)base.VisitSimpleLambdaExpression(node)!;
            return visited.ExpressionBody is { } body ? visited.WithExpressionBody(this.Wrap(body)) : visited;
        }

        public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node) {
            var visited = (ParenthesizedLambdaExpressionSyntax)base.VisitParenthesizedLambdaExpression(node)!;
            return visited.ExpressionBody is { } body ? visited.WithExpressionBody(this.Wrap(body)) : visited;
        }

        private ExpressionSyntax Wrap(ExpressionSyntax body) {
            var call = SyntaxFactory.ParseExpression("global::Benchmark.Dynamic.Core.EvaluationProbe.Hit(0,0)");
            var invocation = (InvocationExpressionSyntax)call;
            var number = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,SyntaxFactory.Literal(this.next++));
            return invocation
                .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]{
                    SyntaxFactory.Argument(number),
                    SyntaxFactory.Argument(body.WithoutTrivia())})))
                .WithLeadingTrivia(body.GetLeadingTrivia())
                .WithTrailingTrivia(body.GetTrailingTrivia());
        }
    }
}

public static class EvaluationCountComparer {
    /// <summary>
    /// 数えた回数を比べる。最適化ありの FLinq は、System.Linq より多く評価してはいけない。
    /// 少ない分には構わない(.Anchor の無いラムダは評価の回数を変えてよい)。
    /// 最適化なしの FLinq は代数的な書き換え(Skip を Select の前へ移す・OrderBy の後の Any の簡約など)を行わないので、System.Linq が評価を省く形で多くなる。
    /// そのため最適化なしと ZLinq は参考値で、合否には使わない。
    /// 戻り値は、問題の説明(問題なしなら null)。
    /// </summary>
    /// <summary>
    /// ラムダごとに比べる。どのラムダでも、最適化ありの FLinq が System.Linq より多く評価してはいけない
    /// (合計が少なくても、System.Linq が評価しない要素にあるラムダを評価すると、例外の有無が変わりうる)。
    /// </summary>
    public static string? CompareByLambda(IReadOnlyDictionary<DynamicImplementationKind,long[]> counts) {
        if(!counts.TryGetValue(DynamicImplementationKind.System,out var expected)
            ||!counts.TryGetValue(DynamicImplementationKind.FLinq,out var actual)) {
            return null;
        }
        var problems = new List<string>();
        for(var lambda = 0;lambda<actual.Length;lambda++) {
            var systemCount = lambda<expected.Length ? expected[lambda] : 0;
            if(actual[lambda]>systemCount) {
                problems.Add($"ラムダ #{lambda}: FLinq {actual[lambda]} 回 > System.Linq {systemCount} 回");
            }
        }
        return problems.Count==0 ? null : $"FLinq は System.Linq より多くラムダを評価しました({string.Join("、",problems)})。";
    }

    public static string? Compare(IReadOnlyDictionary<DynamicImplementationKind,long> counts) {
        if(!counts.TryGetValue(DynamicImplementationKind.System,out var expected)) {
            return null;
        }
        var problems = new List<string>();
        foreach(var kind in new[] { DynamicImplementationKind.FLinq }) {
            if(counts.TryGetValue(kind,out var actual)&&actual>expected) {
                problems.Add($"{kind} は System.Linq より多くラムダを評価しました({actual} 回 > {expected} 回)。上限(Take)や飛ばす要素(Skip)の次の要素などを評価している疑いがあります。");
            }
        }
        return problems.Count==0 ? null : string.Join(" ",problems);
    }
}
