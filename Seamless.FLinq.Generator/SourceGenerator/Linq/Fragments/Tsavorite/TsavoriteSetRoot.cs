namespace Seamless.FLinq.Generator;

internal sealed class TsavoriteSetRoot(
    int 番号,
    string sourceExpression,
    string elementType,
    string enumeratorTypeName)
    :ProviderNativeRootBase(番号,sourceExpression,elementType,enumeratorTypeName,useEnumerableInterface: true) {
    public override string ProviderName => "TsavoriteSet";
    public override string SourceVariablePrefix => "__tsavoriteSetIterateSource";
    public override string StrategyInterfaceTypeName => "global::Seamless.Collections.Iteration.IStrategy";
    // Set<TKey,TRecord>.Contains はキー索引の直接検索。native scan の O(n) を回避する。
    public override string? NativeContainsExpression(string valueExpression) =>
        $"({this.SourceExpression}).Contains({valueExpression})";
}
