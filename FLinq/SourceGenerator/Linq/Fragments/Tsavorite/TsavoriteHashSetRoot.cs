namespace Seamless.FLinq.Linq.Fragments;

/*
TsavoriteHashSetRoot は Tsavorite HashSet を通常 Root と同じ enumerator source として扱いつつ、
専用 scan terminal が元の sourceExpression にアクセスできるよう保持する root。

入力例:
  tsavoriteSet.Where(x => x > 0).Count()

生成例:
  通常経路では Root と同じく GetEnumerator() を包む。
  TsavoriteHashSetScanTerminal が成立する場合だけ、sourceExpression を使って HashSetScanUnsafe.ScanInt32 へ直結する。

設計意図:
  通常 LINQ Fragment と同じ木に載せながら、Tsavorite 固有の内部走査 API へ落とすための印として使う。
*/
public sealed class TsavoriteHashSetRoot(
    int 番号,
    string sourceExpression,
    string elementType,
    string enumeratorTypeName)
    : Root(番号,sourceExpression,elementType,enumeratorTypeName,useEnumerableInterface: false) {
    public string SourceExpression { get; } = sourceExpression;
}
