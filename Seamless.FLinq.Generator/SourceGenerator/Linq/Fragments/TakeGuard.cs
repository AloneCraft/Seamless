namespace Seamless.FLinq.Generator;

/// <summary>
/// Take(n) は n 個目を通した時点で列挙を終える(System.Linq は n+1 個目の上流を評価しない)。
/// 段ごとに「Take の手前で上限を調べる」だけだと、Take より上流の Select / Where が n+1 個目の要素に対して先に実行されてしまう
/// (<c>v=>10/(v-3)</c> のような副作用・例外を持つ式が、System.Linq では呼ばれない要素で呼ばれる)。
/// これを避けるため、Take より手前にある最初のラムダ段の前に「上限に達していれば抜ける」判定を置く。
/// </summary>
internal static class TakeGuard {
    /// <summary>
    /// ラムダを呼ぶ段(Where / Select / TakeWhile / SkipWhile / DistinctBy)。
    /// 添字つきの段は個別の数え方があるので対象にしない。
    /// </summary>
    private static bool IsLambdaStage(PipelineStage stage) =>
        stage.Func is not null
        &&!stage.UsesIndex
        &&stage.Kind is PipelineStageKind.Where or PipelineStageKind.Select or PipelineStageKind.TakeWhile or PipelineStageKind.SkipWhile or PipelineStageKind.DistinctBy;

    // 1つの繰り返しの本体の中で、段が順に並ぶだけの形。ここにない段(SelectMany・二項の集合演算・Concat・順序・GroupBy など)は
    // 別の繰り返しや抜け方を持つので、その区間をまたぐ Take には手前の判定を足さない。
    private static bool IsSimpleStage(PipelineStage stage) =>
        stage.Kind is PipelineStageKind.Where or PipelineStageKind.Select or PipelineStageKind.Skip or PipelineStageKind.Take
            or PipelineStageKind.Cast or PipelineStageKind.OfType or PipelineStageKind.AsEnumerable
            or PipelineStageKind.TakeWhile or PipelineStageKind.SkipWhile or PipelineStageKind.Distinct or PipelineStageKind.DistinctBy;

    /// <summary>
    /// <paramref name="index"/> の段の直前に判定を置く Take 段の位置を返す。
    /// <paramref name="index"/> が、その Take より手前で最初のラムダ段のときだけ、その Take を返す。
    /// </summary>
    internal static IEnumerable<int> TakesGuardedBefore(IReadOnlyList<PipelineStage> stages,int index) {
        if(index<0||index>=stages.Count||!IsLambdaStage(stages[index])) {
            return [];
        }
        for(var k = 0;k<index;k++) {
            if(!IsSimpleStage(stages[k])||IsLambdaStage(stages[k])) {
                return [];
            }
        }
        List<int>? takes = null;
        for(var j = index+1;j<stages.Count;j++) {
            if(!IsSimpleStage(stages[j])) {
                break;
            }
            if(stages[j].Kind==PipelineStageKind.Take&&!stages[j].UsesIndex&&stages[j].Value is not null) {
                (takes??=[]).Add(j);
            }
        }
        return takes??(IEnumerable<int>)[];
    }
}
