namespace Seamless.FLinq.Linq.Fragments;

/*
TsavoriteHashSetRoot keeps the original HashSet expression available for provider-native
terminal fragments. Tsavorite native terminals must call HashSet.Iterate(ref state),
not ScanCursor or legacy unsafe delegate helpers, so generated LINQ sees the logical
collection state instead of raw log records.
*/
public sealed class TsavoriteHashSetRoot(
    int 逡ｪ蜿ｷ,
    string sourceExpression,
    string elementType,
    string enumeratorTypeName)
    : Root(逡ｪ蜿ｷ,sourceExpression,elementType,enumeratorTypeName,useEnumerableInterface: false) {
    public string SourceExpression { get; } = sourceExpression;
}
