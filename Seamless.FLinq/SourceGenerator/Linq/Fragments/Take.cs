namespace Seamless.FLinq.Linq.Fragments;

/*
Take は source.Take(count) を、返した件数を数える enumerator へ変換する。

入力例:
  source.Take(10)
  source.Take(a).Take(b)

生成例:
  if(returned >= count) return false;
  if(source.TryGetNext(out var v)) {
      returned++;
      current = v;
      return true;
  }
  return false;

バリエーション:
  - count < 0 は constructor で 0 に丸める。
  - 連続 Take は RootSource へ畳み込み、count は Math.Min(previous, current) にする。
*/
internal class Take(int 番号,Enumerable source,string takeCount):Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length==1&&method.Parameters[0].Type.SpecialType==SpecialType.System_Int32;
    private Enumerable Source => source;
    private string TakeCountExpression => takeCount;
    private Enumerable RootSource => this.Source is Take take ? take.RootSource : this.Source;
    private string EffectiveTakeCount => this.Source is Take take
        ? $"global::System.Math.Min({take.EffectiveTakeCount},{this.TakeCountExpression})"
        : this.TakeCountExpression;
    protected override Enumerable PrimarySource => this.RootSource;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{this.RootSource.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({this.RootSource.GetEnumeratorExpression}, {this.EffectiveTakeCount})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}, int count";
    public override string GetEnumeratorFields => $$"""
        private int {{this.a}} = 0;
        """;
    public override string GetEnumeratorConstructor => "if(this.count < 0) { this.count = 0; }";
    public override string GetTryGetNextBody => $$"""
        if(this.{{this.a}} >= this.count) {
            current = default!;
            return false;
        }
        if({{this.e}}.TryGetNext(out var __mn75)) {
            this.{{this.a}}++;
            current = this.{{this.currentValue}} = __mn75;
            return true;
        }
        current = default!;
        return false;
        """;
    public override string ElementType => this.Source.ElementType;
    public override string GetConsumeExecutionStatement(string consumerName) {
        var sourceName = $"__takeSource{番号}";
        var spanExpression = this.RootSource.GetDirectSpanExpression(sourceName);
        if(spanExpression is null) {
            return base.GetConsumeExecutionStatement(consumerName);
        }
        return $$"""
            using var {{sourceName}}={{this.RootSource.GetEnumeratorExpression}};
            var __takeSpan{{番号}}={{spanExpression}};
            var __takeCount{{番号}}=global::System.Math.Clamp({{this.EffectiveTakeCount}},0,__takeSpan{{番号}}.Length);
            for(var __takeIndex{{番号}}=0;__takeIndex{{番号}}<__takeCount{{番号}};__takeIndex{{番号}}++){
                {{consumerName}}.Consume(__takeSpan{{番号}}[__takeIndex{{番号}}]);
            }
            """;
    }
}
