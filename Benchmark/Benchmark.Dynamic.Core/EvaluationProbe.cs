using System.Runtime.CompilerServices;

namespace Benchmark.Dynamic.Core;

/// <summary>
/// 評価回数の検査(<see cref="QueryEvaluationCheck"/>)のために、生成したクエリのラムダの本体を包んで、実行された回数をラムダごとに数える。
/// 生成コードから <c>global::Benchmark.Dynamic.Core.EvaluationProbe.Hit(番号,式)</c> として呼ばれる。番号は、クエリの式の中のラムダの出現順
/// (System.Linq と FLinq で同じ式を出すので、同じ番号は同じラムダ)。
/// クエリは1つずつ直列に実行されるので、数えるのは単純な静的な値でよい(実行の前に <see cref="Reset"/> する)。
/// FLinq の生成器は <see cref="FLinqTransparentAttribute"/> を見て、この呼び出しを恒等とみなす(本体の形を見る最適化が、数える形でも効く)。
/// </summary>
public static class EvaluationProbe{
    private const int MaximumLambdas=256;
    private static readonly long[] counts=new long[MaximumLambdas];
    private static int used;

    // 全部のラムダの合計。
    public static long Count{
        get{
            long total=0;
            for(var index=0;index<used;index++){
                total+=counts[index];
            }
            return total;
        }
    }

    // ラムダの番号ごとの回数(番号 0 から、数えた中の最大の番号まで)。
    public static long[] CountsByLambda=>counts.AsSpan(0,used).ToArray();

    public static void Reset(){
        Array.Clear(counts);
        used=0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [FLinqTransparent]
    public static T Hit<T>(int lambda,T value){
        counts[lambda]++;
        if(lambda>=used){
            used=lambda+1;
        }
        return value;
    }
}

/// <summary>
/// FLinq の生成器が、名前で見分ける目印(名前空間は問わない)。付いた静的メソッドは、最後の引数をそのまま返す「恒等の包み」とみなされる。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class FLinqTransparentAttribute:Attribute{
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
