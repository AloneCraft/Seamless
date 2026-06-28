namespace Seamless.FLinq.Linq.Fragments;
/*
MinMaxBase は selector なし Min/Max を ValueEnumerableExtensions.Min/Max へつなぐ基底。

入力例:
  source.Min()
  source.Max()

生成例:
  using var e = source.GetEnumerator();
  result = direction == "greater" ? Max(e) : Min(e);

バリエーション:
  - direction=="greater" は Max、"less" は Min。
  - RangeRoot<int> は最小/最大値を式で inline できる。
  - DirectEnumerableExpression があれば System.Linq.Min/Max へ委譲する。
*/
public abstract class MinMaxBase(int 番号, Enumerable source, string resultType, string direction) : BufferedScalar(番号, resultType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => direction=="greater"
        ? $"global::Seamless.System.Carrier.ValueEnumerableExtensions.Max(new global::Seamless.System.Carrier.ValueEnumerable<TEnum, {source.ElementType}>({this.e}))"
        : $"global::Seamless.System.Carrier.ValueEnumerableExtensions.Min(new global::Seamless.System.Carrier.ValueEnumerable<TEnum, {source.ElementType}>({this.e}))";
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";

    // 素の Min/Max は Compute メソッドを持たず、ライブラリ拡張＋ジェネリック TEnum を使うため独自インライン。
    // TEnum を具体的なソース列挙子型へ、this.e をその場のローカルへ差し替える。
    public override string? RenderInlineComputation(string destVar,string destType){
        var methodName = direction=="greater" ? "Max" : "Min";
        if(source is RangeRoot range && source.ElementType=="int") {
            return RangePredicateOptimizer.RenderMinMax(range,番号,destVar,destType,direction=="greater");
        }
        if(this.RenderDirectEnumerableCallInline(source,destVar,destType,methodName) is { } direct) {
            return direct;
        }
        string srcExpr;
        try { srcExpr = source.GetEnumeratorExpression; }
        catch(NotSupportedException) { return null; }
        var srcVar = $"__s{番号}";
        var call = this.ComputeExpression
            .Replace("TEnum",source.EnumeratorTypeName)
            .Replace(this.e,srcVar);
        return $$"""
            {{destType}} {{destVar}};
            using(var {{srcVar}}={{srcExpr}}){
                {{destVar}}={{call}};
            }
            """;
    }
    public override string InlineSupportingMembers => source.GetEnumeratorStruct;
}
