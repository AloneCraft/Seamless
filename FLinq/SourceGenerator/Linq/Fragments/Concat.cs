namespace Seamless.FLinq.Linq.Fragments;

/*
Concat は source.Concat(second) を、左を読み切ってから右へ切り替える2入力 enumerator にする。

入力例:
  first.Concat(second)

生成例:
  if(!readingSecond && first.MoveNext()) return first.Current;
  readingSecond = true;
  if(second.MoveNext()) return second.Current;
  return false;

バリエーション:
  - 左右の要素型が異なる場合でも、最終 ElementType は左に合わせて cast する。
  - Dispose では両方の enumerator を破棄する。
*/
public class Concat(int 番号, Enumerable source, Enumerable second) : Enumerable(番号) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    public override string GetEnumerableExpression =>
        $"global::Seamless.Collections.Tsavorite.Carrier.ValueEnumerableExtensions.Concat({source.GetEnumerableExpression}, {second.GetEnumerableExpression})";
    protected override string StructTypeParameters => "<TEnum1, TEnum2>";
    protected override string StructTypeConstraints =>
        $"where TEnum1 : struct, global::System.Collections.Generic.IEnumerator<{source.ElementType}>\n    where TEnum2 : struct, global::System.Collections.Generic.IEnumerator<{second.ElementType}>";
    public override string EnumeratorTypeName => $"{this.Struct}<{source.EnumeratorTypeName}, {second.EnumeratorTypeName}>";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{second.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {second.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum1 source, TEnum2 second";
    public override string GetEnumeratorFields => $$"""
        private bool {{this.hasValue}};
        """;
    public override string GetTryGetNextBody => $$"""
        if(!this.{{this.hasValue}}) {
            if(source.MoveNext()) {
                current=this.{{this.currentValue}}=source.Current;
                return true;
            }
            this.{{this.hasValue}}=true;
        }
        if(second.MoveNext()) {
            current=this.{{this.currentValue}}=({{source.ElementType}})(object)second.Current!;
            return true;
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => source.ElementType;
    protected override string GetEnumeratorDisposeBody => "source.Dispose();\nsecond.Dispose();";
}
