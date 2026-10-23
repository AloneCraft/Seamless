using System.Runtime.CompilerServices;

namespace Benchmark.Dynamic.Core;

/// <summary>
/// 評価回数の検査(<see cref="QueryEvaluationCheck"/>)のために、生成したクエリのラムダの本体を包んで、実行された回数を数える。
/// 生成コードから <c>global::Benchmark.Dynamic.Core.EvaluationProbe.Hit(式)</c> として呼ばれる。
/// クエリは1つずつ直列に実行されるので、数えるのは単純な静的な値でよい(実行の前に <see cref="Reset"/> する)。
/// </summary>
public static class EvaluationProbe{
    private static long count;

    public static long Count=>Volatile.Read(ref count);

    public static void Reset()=>Volatile.Write(ref count,0);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static T Hit<T>(T value){
        count++;
        return value;
    }
}

/// <summary>
/// 評価回数の検査を行う問い合わせか。Take・Skip・Zip・First・Any のように、途中で止まる(または要素を飛ばす)演算が含まれるときだけ行う。
/// 結果の一致だけを比べても、System.Linq が評価しない要素に FLinq がラムダを実行しても、結果は同じに見える(ラムダが例外も副作用も出さないため)。
/// 回数を数えれば、その余分な評価を見つけられる。
/// </summary>
public static class QueryEvaluationCheck{
    private static readonly HashSet<string> EarlyExitOperatorIds=new(StringComparer.Ordinal){
        QueryOperatorIds.Take,
        QueryOperatorIds.Skip,
        QueryOperatorIds.TakeWhile,
        QueryOperatorIds.Zip,
        QueryOperatorIds.Any,
        QueryOperatorIds.AnyWhere,
        QueryOperatorIds.All,
        QueryOperatorIds.Contains,
        QueryOperatorIds.First,
        QueryOperatorIds.FirstOrDefault,
        QueryOperatorIds.ElementAt,
        QueryOperatorIds.ElementAtOrDefault,
        QueryOperatorIds.SequenceEqual,
    };

    public static bool Applies(QueryIrDocument document){
        ArgumentNullException.ThrowIfNull(document);
        return Applies(document.Root);
    }

    private static bool Applies(QueryNode node){
        if(EarlyExitOperatorIds.Contains(node.OperatorId)){
            return true;
        }
        foreach(var child in node.Children){
            if(Applies(child)){
                return true;
            }
        }
        return false;
    }
}
