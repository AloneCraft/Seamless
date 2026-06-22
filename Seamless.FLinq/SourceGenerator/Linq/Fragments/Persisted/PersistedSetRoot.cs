namespace Seamless.FLinq.Linq.Fragments;

public sealed class PersistedSetRoot(
    int 逡ｪ蜿ｷ,
    string sourceExpression,
    string elementType,
    string enumeratorTypeName)
    : Root(逡ｪ蜿ｷ,sourceExpression,elementType,enumeratorTypeName,useEnumerableInterface: false) {
    public string SourceExpression { get; } = sourceExpression;
}
