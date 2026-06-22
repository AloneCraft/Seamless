namespace Seamless.FLinq.Linq.Fragments;

public sealed class TsavoriteHashSetRoot(
    int 番号,
    string sourceExpression,
    string elementType,
    string enumeratorTypeName)
    : Root(番号,sourceExpression,elementType,enumeratorTypeName,useEnumerableInterface: false) {
    public string SourceExpression { get; } = sourceExpression;
}
