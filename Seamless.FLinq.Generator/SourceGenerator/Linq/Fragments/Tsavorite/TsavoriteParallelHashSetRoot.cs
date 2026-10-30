namespace Seamless.FLinq.Generator;

// Tsavorite のハッシュセットのマルチ用(ParallelHashSet<T>)。件数を持たないので、件数の式は null にして
// 先取りや TryGetNonEnumeratedCount の即答をしない。素の Count は要素を取り出さずに数える ScanCount へ任せる。
internal sealed class TsavoriteParallelHashSetRoot(
    int 番号,
    string sourceExpression,
    string elementType,
    string enumeratorTypeName)
    :ProviderNativeRootBase(番号,sourceExpression,elementType,enumeratorTypeName,useEnumerableInterface: false) {
    public override string ProviderName => "TsavoriteParallelHashSet";
    public override string SourceVariablePrefix => "__tsavoriteParallelIterateSource";
    public override string StrategyInterfaceTypeName => "global::Seamless.Collections.Iteration.IStrategy";
    public override string? NativeCountExpression => null;
    public override string? ScanCountMethodName => "ScanCount";
    // ParallelHashSet<T>.Contains はハッシュ索引の直接検索。native scan の O(n) を回避する。
    public override string? NativeContainsExpression(string valueExpression) =>
        $"({this.SourceExpression}).Contains({valueExpression})";
}
