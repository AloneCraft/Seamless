namespace Seamless.FLinq.Linq.Fragments;

/*
Reverse は source.Reverse() を、初回に逆順で読める状態を作ってから末尾から返す enumerator へ変換する。

入力例:
  source.Reverse()
  source.Reverse().Reverse()

生成例:
  if(notInitialized) {
      if(source.TryGetSpan(out var span)) index = span.Length - 1;
      else buffer = source.ToList(); index = buffer.Count - 1;
  }
  if(index >= 0) return spanOrBuffer[index--];
  return false;

バリエーション:
  - source が span を返せる場合は buffer を作らず span の末尾から読む。
  - span が無い場合は List に全件 buffer する。
  - 連続 Reverse は偶数回なら RootSource そのものへ畳み込む。
*/
public class Reverse(int 番号, Enumerable source) : Enumerable(番号) {
    private Enumerable Source => source;
    private Enumerable RootSource => Source is Reverse reverse ? reverse.RootSource : Source;
    private bool IsIdentity => Source is Reverse reverse && !reverse.IsIdentity;
    protected override Enumerable PrimarySource => RootSource;
    public override string StructMethod => string.Empty;
    public override string GetEnumerableExpression =>
        IsIdentity
            ? RootSource.GetEnumerableExpression
            : $"global::Seamless.System.Carrier.ValueEnumerableExtensions.Reverse({RootSource.GetEnumerableExpression})";
    public override string GetEnumeratorStruct => $$"""
        {{RootSource.GetEnumeratorStruct}}
        {{(IsIdentity ? string.Empty : base.GetEnumeratorStruct)}}
        """;
    public override string EnumeratorTypeName => IsIdentity
        ? RootSource.EnumeratorTypeName
        : base.EnumeratorTypeName;
    public override string GetEnumeratorExpression => IsIdentity
        ? RootSource.GetEnumeratorExpression
        : $"new {EnumeratorTypeName}({RootSource.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}";
    public override string GetEnumeratorFields => $$"""
        private int {{a}}=int.MinValue;
        private global::System.Collections.Generic.List<{{RootSource.ElementType}}>? {{group}};
        """;
    public override string GetTryGetNextBody => RootSource.CanTryProvideSpan
        ? $$"""
            // {{a}} は int.MinValue=未初期化 / -1=列挙完了 を区別する。
            // -1 を未初期化扱いにすると、TryGetNext を完了後も呼ぶ消費側（TraversalSupport の while ループ等）で
            // 末尾到達ごとに Length-1 へ再初期化して無限ループになる。
            if(this.{{a}}==int.MinValue) {
                if({{e}}.TryGetSpan(out var __span0)) {
                    this.{{a}}=__span0.Length-1;
                }else{
                    if({{e}}.TryGetNonEnumeratedCount(out var __count)) {
                        this.{{group}}=new global::System.Collections.Generic.List<{{RootSource.ElementType}}>(__count);
                    }else{
                        this.{{group}}=new global::System.Collections.Generic.List<{{RootSource.ElementType}}>();
                    }
                    while({{e}}.MoveNext()) this.{{group}}.Add({{e}}.Current);
                    this.{{a}}=this.{{group}}.Count-1;
                }
            }
            if(this.{{a}}>=0) {
                if({{e}}.TryGetSpan(out var __span)) {
                    current=this.{{currentValue}}=__span[this.{{a}}--];
                }else{
                    current=this.{{currentValue}}=this.{{group}}![this.{{a}}--];
                }
                return true;
            }
            current=default!;
            return false;
            """
        : $$"""
            if(this.{{group}} is null) {
                if({{e}}.TryGetNonEnumeratedCount(out var __count)) {
                    this.{{group}}=new global::System.Collections.Generic.List<{{RootSource.ElementType}}>(__count);
                }else{
                    this.{{group}}=new global::System.Collections.Generic.List<{{RootSource.ElementType}}>();
                }
                while({{e}}.MoveNext()) this.{{group}}.Add({{e}}.Current);
                this.{{a}}=this.{{group}}.Count-1;
            }
            if(this.{{a}}>=0) {
                current=this.{{currentValue}}=this.{{group}}[this.{{a}}--];
                return true;
            }
            current=default!;
            return false;
            """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => RootSource.ElementType;
}
