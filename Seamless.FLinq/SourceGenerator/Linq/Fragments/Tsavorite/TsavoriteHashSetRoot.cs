namespace Seamless.FLinq.Linq.Fragments;

/*
TsavoriteHashSetRoot keeps the original HashSet expression available for provider-native
terminal fragments. Tsavorite native terminals must call HashSet.Iterate(ref state),
not raw log traversal or legacy unsafe delegate helpers, so generated LINQ sees the logical
collection state instead of raw log records.
*/
internal sealed class TsavoriteHashSetRoot(
    int 番号,
    string sourceExpression,
    string elementType,
    string enumeratorTypeName)
    :ProviderNativeRootBase(番号,sourceExpression,elementType,enumeratorTypeName,useEnumerableInterface: false) {
    public override string ProviderName => "TsavoriteHashSet";
    public override string SourceVariablePrefix => "__tsavoriteIterateSource";
    public override string StrategyInterfaceTypeName => "global::Seamless.Tsavorite.Collections.IHashSetIterateStrategy";
    // HashSet<T>.Contains はハッシュ索引の直接検索。native scan の O(n) を回避する。
    public override string? NativeContainsExpression(string valueExpression) =>
        $"({this.SourceExpression}).Contains({valueExpression})";
}
