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
        // ZLinq 1.5.6 は TakeWhile の結果が空のとき、直後の DefaultIfEmpty が既定値のほかに余計な要素を返す
        // (takeWhile > defaultIfEmpty の全組み合わせの検証で見つかった。System.Linq と FLinq は一致している)。
        if(node is UnaryQueryNode{Arguments:DefaultIfEmptyOperatorArguments,Input:UnaryQueryNode{Arguments:TakeWhileOperatorArguments}}){
            return false;
        }
        // ZLinq 1.5.6 は TakeWhile の述語が最初の要素で偽でも、Select を挟んだ FirstOrDefault が、最初の要素を選択した値を返す
        // (評価回数の検査の中で見つかった。takeWhile > select > firstOrDefault で System.Linq・FLinq は既定値、ZLinq は選択した値)。
        if(node is TerminalQueryNode{OperatorId:QueryOperatorIds.First or QueryOperatorIds.FirstOrDefault,Input:UnaryQueryNode{Arguments:SelectOperatorArguments,Input:UnaryQueryNode{Arguments:TakeWhileOperatorArguments}}}){
            return false;
        }
        return node.Children.All(IsSupported);
    }
}
