namespace Benchmark.Dynamic.Core;

/// <summary>
/// ZLinq が対応しない演算子を含むクエリでは、ZLinq の経路を作らず System と FLinq だけで比べる。
/// ZLinq 1.5.6 の OrderBy 系は整列結果の具体化で例外になる(CapabilityProfile の注記と同じ理由)。
/// </summary>
public static class QueryZLinqSupport{
    public static bool IsSupported(QueryIrDocument document){
        ArgumentNullException.ThrowIfNull(document);
        return IsSupported(document.Root);
    }

    public static bool IsSupported(QueryNode node){
        ArgumentNullException.ThrowIfNull(node);
        if(node is UnaryQueryNode{Arguments:OrderByOperatorArguments or ThenByOperatorArguments or OrderByKeyOperatorArguments}){
            return false;
        }
        return node.Children.All(IsSupported);
    }
}
