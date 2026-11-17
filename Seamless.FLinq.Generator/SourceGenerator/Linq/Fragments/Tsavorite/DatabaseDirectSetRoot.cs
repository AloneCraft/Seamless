namespace Seamless.FLinq.Generator;

// DB の取引を通さない表(db.X、Seamless.Database.Set<TKey,TRecord>・Set<TRecord>)。
// 走査(Iterate)の間は表がスレッドの置き場を使用中にし、件数は区間を開いたときの件数に各スレッドの増減を足した値を O(1) で返す。
// 設計：Document/refactoring-designs/20261115-取引を通さない表の作り直し.md
internal sealed class DatabaseDirectSetRoot(
    int 番号,
    string sourceExpression,
    string elementType,
    string enumeratorTypeName)
    :ProviderNativeRootBase(番号,sourceExpression,elementType,enumeratorTypeName,useEnumerableInterface: true) {
    public override string ProviderName => "DatabaseDirectSet";
    public override string SourceVariablePrefix => "__databaseDirectSetIterateSource";
    public override string StrategyInterfaceTypeName => "global::Seamless.Collections.Iteration.IStrategy";
    // Set<TKey,TRecord>.Contains はキー索引、Set<TRecord>.Contains はハッシュ索引の直接検索。native scan の O(n) を回避する。
    public override string? NativeContainsExpression(string valueExpression) =>
        $"({this.SourceExpression}).Contains({valueExpression})";
}
