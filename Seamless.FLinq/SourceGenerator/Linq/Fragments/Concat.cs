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
public class Concat(int 番号,Enumerable source,Enumerable second):Enumerable(番号) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    public override string GetEnumerableExpression =>
        $"global::Seamless.Linq.Enumerable.Concat({source.GetEnumerableExpression}, {second.GetEnumerableExpression})";
    protected override string StructTypeParameters => this.StructTypeParametersWithOuter("TEnum1","TEnum2");
    protected override string StructTypeConstraints =>
        $"where TEnum1 : struct, global::System.Collections.Generic.IEnumerator<{source.ElementType}>\n    where TEnum2 : struct, global::System.Collections.Generic.IEnumerator<{second.ElementType}>";
    public override string EnumeratorTypeName => this.EnumeratorTypeNameWithOuter(source.EnumeratorTypeName,second.EnumeratorTypeName);
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
    public override string GetConsumeExecutionStatement(string consumerName) {
        if(source is RangeRoot leftRange&&second is RangeRoot rightRange) {
            return $$"""
                {
                    var __leftStart{{番号}}={{leftRange.StartExpression}};
                    var __leftCount{{番号}}={{leftRange.CountExpression}};
                    for(var __i{{番号}}=0;__i{{番号}}<__leftCount{{番号}};__i{{番号}}++){
                        {{consumerName}}.Consume(__leftStart{{番号}}+__i{{番号}});
                    }
                    var __rightStart{{番号}}={{rightRange.StartExpression}};
                    var __rightCount{{番号}}={{rightRange.CountExpression}};
                    for(var __i{{番号}}=0;__i{{番号}}<__rightCount{{番号}};__i{{番号}}++){
                        {{consumerName}}.Consume(__rightStart{{番号}}+__i{{番号}});
                    }
                }
                """;
        }
        var sourceName = $"__concatSrc{番号}";
        var secondName = $"__concatSec{番号}";
        var secondConsumeStatement = source.ElementType==second.ElementType
            ? $"{consumerName}.Consume(value);"
            : $"{consumerName}.Consume(({source.ElementType})(object?)value!);";
        var builder = new StringBuilder();
        builder.AppendLine($"using var {sourceName}={source.GetEnumeratorExpression};");
        builder.AppendLine($"using var {secondName}={second.GetEnumeratorExpression};");
        TraversalSupport.AppendTraversalLoop(
            builder,
            sourceName,
            source.ElementType,
            "value",
            $"{consumerName}.Consume(value);",
            source.CanTryProvideSpan,
            source.CanProvideSpan,
            source.GetDirectSpanExpression(sourceName));
        TraversalSupport.AppendTraversalLoop(
            builder,
            secondName,
            second.ElementType,
            "value",
            secondConsumeStatement,
            second.CanTryProvideSpan,
            second.CanProvideSpan,
            second.GetDirectSpanExpression(secondName));
        return builder.ToString().TrimEnd();
    }
}
