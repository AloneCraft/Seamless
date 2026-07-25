namespace Seamless.FLinq.Linq.Fragments;

public sealed class PersistedSetRoot(
    int 番号,
    string sourceExpression,
    string elementType,
    string enumeratorTypeName)
    :ProviderNativeRootBase(番号,sourceExpression,elementType,enumeratorTypeName,useEnumerableInterface: false) {
    public override string ProviderName => "PersistedSet";
    public override string SourceVariablePrefix => "__persistedIterateSource";
    public override string StrategyInterfaceTypeName => "global::Seamless.Persisted.Collections.ISetIterateStrategy";
    // Set<T>.Contains は .hindex（mmap ハッシュ索引）で O(1)。native scan の O(n) を回避する。
    public override string? NativeContainsExpression(string valueExpression) =>
        $"({this.SourceExpression}).Contains({valueExpression})";
}
