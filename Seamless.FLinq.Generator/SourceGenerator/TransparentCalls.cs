namespace Seamless.FLinq.Generator;

/// <summary>
/// 「恒等の包み」の呼び出しを見通す。名前が FLinqTransparentAttribute の属性(名前空間は問わない)が付いた静的メソッドで、
/// 戻り値の型が最後の引数の型と同じものは、最後の引数をそのまま返すとみなす(最後より前の引数は定数に限る)。
/// 評価回数の検査(Benchmark.Dynamic の EvaluationProbe.Hit)のように、ラムダの本体を数えるためだけに包む呼び出しがあっても、
/// 本体の形を見る最適化(恒等の Select を除く・SelectMany の内側の Select を除く など)が効くようにする。
/// 包みは .Anchor の無いラムダと同じく、評価の回数を減らしてよい(呼ばれないこともある)。
/// </summary>
internal static class TransparentCalls {
    private const string AttributeName = "FLinqTransparentAttribute";

    // 恒等の包みを外側から順に剥がした式を返す。包みでなければ expression をそのまま返す。
    public static ExpressionSyntax Peel(ExpressionSyntax expression,SemanticModel? semanticModel) {
        if(semanticModel is null) {
            return expression;
        }
        while(StripParentheses(expression) is InvocationExpressionSyntax invocation
            &&invocation.ArgumentList.Arguments.Count>0
            &&IsTransparentCall(invocation,semanticModel)) {
            expression=invocation.ArgumentList.Arguments[invocation.ArgumentList.Arguments.Count-1].Expression;
        }
        return expression;
    }

    // body が、包みを剥がすとラムダの引数 parameterName そのものになるか(包みが1つも無いときは false。それは通常の恒等の判定に任せる)。
    public static bool IsWrappedParameter(ExpressionSyntax body,string parameterName,SemanticModel? semanticModel) {
        var peeled = Peel(body,semanticModel);
        return !ReferenceEquals(peeled,body)
            &&StripParentheses(peeled) is IdentifierNameSyntax identifier
            &&identifier.Identifier.ValueText==parameterName;
    }

    private static bool IsTransparentCall(InvocationExpressionSyntax invocation,SemanticModel semanticModel) {
        SymbolInfo info;
        try {
            info=semanticModel.GetSymbolInfo(invocation);
        } catch(ArgumentException) {
            // 書き換えで作った、意味モデルの木に無いノード。
            return false;
        }
        if(info.Symbol is not IMethodSymbol method
            ||!method.IsStatic
            ||method.Parameters.Length!=invocation.ArgumentList.Arguments.Count
            ||!SymbolEqualityComparer.Default.Equals(method.ReturnType,method.Parameters[method.Parameters.Length-1].Type)
            ||!method.OriginalDefinition.GetAttributes().Any(static attribute => attribute.AttributeClass?.Name==AttributeName)) {
            return false;
        }
        var arguments = invocation.ArgumentList.Arguments;
        for(var index = 0;index<arguments.Count-1;index++) {
            if(!semanticModel.GetConstantValue(arguments[index].Expression).HasValue) {
                return false;
            }
        }
        return true;
    }

    private static ExpressionSyntax StripParentheses(ExpressionSyntax expression) {
        while(expression is ParenthesizedExpressionSyntax parenthesized) {
            expression=parenthesized.Expression;
        }
        return expression;
    }
}
