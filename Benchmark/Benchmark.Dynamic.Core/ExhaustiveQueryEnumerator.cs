namespace Benchmark.Dynamic.Core;

/// <summary>固定した演算子の並びが、その位置では適用できない(例: 順序なし入力への Take)ことを表す。</summary>
public sealed class ForcedOperatorNotApplicableException(string message):InvalidOperationException(message);

/// <summary>
/// 全組み合わせの1件。<see cref="Operators"/> は Source に近い側から並べた中間演算子、
/// <see cref="Terminal"/> は終端演算子。
/// </summary>
public sealed record ExhaustiveQuerySpec(long Ordinal,IReadOnlyList<string> Operators,string Terminal);

/// <summary>
/// 中間演算子を <c>intermediateCount</c> 個並べ、最後に終端演算子を付けた全組み合わせを、番号の順に列挙する。
/// 引数の値は <see cref="RandomQueryGenerator"/> がシードから決めるので、同じ番号とシードなら必ず同じクエリになる。
/// 二項・入れ子の演算子は、右側(内側)を副 Source 1つにして使う。
/// </summary>
public static class ExhaustiveQueryEnumerator{
    public static IReadOnlyList<string> GetIntermediateOperatorIds(SourceTarget target){
        return GetOperatorIds(target)
            .Where(static id=>BuiltInOperatorCatalog.Get(id).NodeKind is QueryNodeKind.Unary or QueryNodeKind.Binary or QueryNodeKind.Nested)
            .ToArray();
    }

    public static IReadOnlyList<string> GetTerminalOperatorIds(SourceTarget target){
        return GetOperatorIds(target)
            .Where(static id=>BuiltInOperatorCatalog.Get(id).NodeKind==QueryNodeKind.Terminal)
            .ToArray();
    }

    public static long Count(int intermediateCount,IReadOnlyList<string> intermediates,IReadOnlyList<string> terminals){
        ArgumentOutOfRangeException.ThrowIfLessThan(intermediateCount,1);
        var total=(long)terminals.Count;
        for(var index=0;index<intermediateCount;index++){
            total=checked(total*intermediates.Count);
        }
        return total;
    }

    public static IEnumerable<ExhaustiveQuerySpec> Enumerate(
        int intermediateCount,
        IReadOnlyList<string> intermediates,
        IReadOnlyList<string> terminals,
        long startOrdinal=0){
        var total=Count(intermediateCount,intermediates,terminals);
        for(var ordinal=startOrdinal;ordinal<total;ordinal++){
            yield return Decode(ordinal,intermediateCount,intermediates,terminals);
        }
    }

    public static ExhaustiveQuerySpec Decode(
        long ordinal,
        int intermediateCount,
        IReadOnlyList<string> intermediates,
        IReadOnlyList<string> terminals){
        var remainder=ordinal;
        var terminal=terminals[(int)(remainder%terminals.Count)];
        remainder/=terminals.Count;
        var operators=new string[intermediateCount];
        for(var index=intermediateCount-1;index>=0;index--){
            operators[index]=intermediates[(int)(remainder%intermediates.Count)];
            remainder/=intermediates.Count;
        }
        return new(ordinal,operators,terminal);
    }

    /// <summary>
    /// 1件をIRにする。その並びが静的に成り立たない場合(順序なし入力への位置依存演算子など)は false。
    /// </summary>
    public static bool TryGenerate(
        ExhaustiveQuerySpec spec,
        Seed256 masterSeed,
        SourceTarget target,
        int inputElementCount,
        out QueryIrDocument? document){
        try{
            document=new RandomQueryGenerator().Generate(new RandomQueryGenerationOptions{
                MasterSeed=masterSeed,
                QueryIndex=checked((int)spec.Ordinal),
                QueryId=$"exhaustive-{spec.Ordinal:D8}",
                SourceTarget=target,
                OperatorCount=spec.Operators.Count+1,
                MaximumDepth=spec.Operators.Count+1,
                InputElementCount=inputElementCount,
                EnableBinaryOperators=true,
                EnableNestedOperators=true,
                ForcedOperatorSequence=spec.Operators,
                ForcedTerminalOperatorId=spec.Terminal,
            });
            return true;
        }catch(ForcedOperatorNotApplicableException){
            document=null;
            return false;
        }
    }

    private static IEnumerable<string> GetOperatorIds(SourceTarget target){
        return QueryCapabilityProfiles.CreateSet(OperatorSetMode.Maximum,[target]).OperatorIds;
    }
}
