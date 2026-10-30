namespace Seamless.FLinq.Generator;

// Tsavorite のレコードの集合のマルチ用(ParallelSet<TKey,TRecord>)。件数を持たない理由と扱いは TsavoriteParallelHashSetRoot と同じ。
internal sealed class TsavoriteParallelSetRoot(
    int 番号,
    string sourceExpression,
    string elementType,
    string enumeratorTypeName)
    :ProviderNativeRootBase(番号,sourceExpression,elementType,enumeratorTypeName,useEnumerableInterface: true) {
    public override string ProviderName => "TsavoriteParallelSet";
    public override string SourceVariablePrefix => "__tsavoriteParallelSetIterateSource";
    public override string StrategyInterfaceTypeName => "global::Seamless.Collections.Iteration.IStrategy";
    public override string? NativeCountExpression => null;
    public override string? ScanCountMethodName => "ScanCount";
    // ParallelSet<TKey,TRecord>.Contains はキー索引の直接検索。native scan の O(n) を回避する。
    public override string? NativeContainsExpression(string valueExpression) =>
        $"({this.SourceExpression}).Contains({valueExpression})";
}
