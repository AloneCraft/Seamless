namespace Seamless.FLinq.Generator;

/*
Concat は source.Concat(second) を、左を読み切ってから右へ切り替える2入力 enumerator にする。

入力例:
  first.Concat(second)

生成例:
  if(!readingSecond && first.TryGetNext(out var v)) return v;
  readingSecond = true;
  if(second.TryGetNext(out var v2)) return v2;
  return false;

バリエーション:
  - 左右の要素型が異なる場合でも、最終 ElementType は左に合わせて cast する。
  - Dispose では両方の enumerator を破棄する。
*/
internal class Concat(int 番号,Enumerable source,Enumerable second):Enumerable(番号) {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
        if(second is { } __visitorChild_second) {
            yield return __visitorChild_second;
        }
    }

    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    public override string GetEnumerableExpression =>
        $"global::Seamless.Linq.Enumerable.Concat({source.GetEnumerableExpression}, {second.GetEnumerableExpression})";
    protected override string StructTypeParameters => this.StructTypeParametersWithOuter("TEnum1","TEnum2");
    protected override string StructTypeConstraints =>
        $"where TEnum1 : struct, global::Seamless.Linq.IValueEnumerator<{source.ElementType}>\n    where TEnum2 : struct, global::Seamless.Linq.IValueEnumerator<{second.ElementType}>";
    public override string EnumeratorTypeName => this.EnumeratorTypeNameWithOuter(source.EnumeratorTypeName,second.EnumeratorTypeName);
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{second.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.EnumeratorTypeName,source.GetEnumeratorExpression,second.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum1 source, TEnum2 second";
    public override string GetEnumeratorFields => $$"""
        private bool {{this.hasValue}};
        """;
    public override string GetTryGetNextBody => $$"""
        if(!this.{{this.hasValue}}){
            if(source.TryGetNext(out var __left)){
                current=this.{{this.currentValue}}=__left;
                return true;
            }
            this.{{this.hasValue}}=true;
        }
        if(second.TryGetNext(out var __right)){
            current=this.{{this.currentValue}}=({{source.ElementType}})(object)__right!;
            return true;
        }
        current=default!;
        return false;
        """;
    public override string ElementType => source.ElementType;
    protected override string GetEnumeratorDisposeBody => "source.Dispose();\nsecond.Dispose();";
    public override string GetConsumePreparationStatement() {
        if(source is RangeRoot leftRange&&second is RangeRoot rightRange) {
            return $$"""
                {{leftRange.RenderArgumentPreparation(this.RangeLeftStartName,this.RangeLeftCountName)}}
                {{rightRange.RenderArgumentPreparation(this.RangeRightStartName,this.RangeRightCountName)}}
                """;
        }
        if(source.DirectEnumerableExpression is not { } directSource
            ||second.DirectEnumerableExpression is not { } directSecond) {
            return string.Empty;
        }
        return $$"""
            var {{this.DirectSourceName}}={{directSource}};
            var {{this.DirectSecondName}}={{directSecond}};
            global::System.ArgumentNullException.ThrowIfNull({{this.DirectSourceName}},"first");
            global::System.ArgumentNullException.ThrowIfNull({{this.DirectSecondName}},"second");
            """;
    }
    public override string GetConsumeExecutionStatement(string consumerName) {
        if(source is RangeRoot leftRange&&second is RangeRoot rightRange) {
            return $$"""
                {
                    for(var __i{{番号}}=0;__i{{番号}}<{{this.RangeLeftCountName}};__i{{番号}}++){
                        {{consumerName}}.Consume({{this.RangeLeftStartName}}+__i{{番号}});
                    }
                    for(var __i{{番号}}=0;__i{{番号}}<{{this.RangeRightCountName}};__i{{番号}}++){
                        {{consumerName}}.Consume({{this.RangeRightStartName}}+__i{{番号}});
                    }
                }
                """;
        }
        if(source.DirectEnumerableExpression is { } directSource
            &&second.DirectEnumerableExpression is { } directSecond) {
            return this.RenderDirectEnumerableConsume(consumerName);
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

    private string DirectSourceName => $"__concatFirstEnumerable{this.result}";
    private string DirectSecondName => $"__concatSecondEnumerable{this.result}";
    private string RangeLeftStartName => $"__concatLeftStart{this.result}";
    private string RangeLeftCountName => $"__concatLeftCount{this.result}";
    private string RangeRightStartName => $"__concatRightStart{this.result}";
    private string RangeRightCountName => $"__concatRightCount{this.result}";

    private string RenderDirectEnumerableConsume(string consumerName) {
        var suffix=this.result;
        var sourceName=this.DirectSourceName;
        var secondName=this.DirectSecondName;
        var sourceEnumeratorName=$"__concatFirstEnumerator{suffix}";
        var secondEnumeratorName=$"__concatSecondEnumerator{suffix}";
        var sourceValueName=$"__concatFirstValue{suffix}";
        var secondValueName=$"__concatSecondValue{suffix}";
        var secondConsumeStatement=source.ElementType==second.ElementType
            ? $"{consumerName}.Consume({secondValueName});"
            : $"{consumerName}.Consume(({source.ElementType})(object?){secondValueName}!);";
        return $$"""
            {
                var {{sourceEnumeratorName}}={{sourceName}}.GetEnumerator();
                try {
                    {{this.CounterCall("IncrementEnumerationLoopUsed")}}
                    while({{sourceEnumeratorName}}.MoveNext()){
                        var {{sourceValueName}}={{sourceEnumeratorName}}.Current;
                        {{consumerName}}.Consume({{sourceValueName}});
                    }
                } finally {
                    {{sourceEnumeratorName}}.Dispose();
                }
                global::System.Collections.Generic.IEnumerator<{{second.ElementType}}> {{secondEnumeratorName}};
                try {
                    {{secondEnumeratorName}}={{secondName}}.GetEnumerator();
                } catch {
                    {{sourceEnumeratorName}}.Dispose();
                    throw;
                }
                {
                    using({{secondEnumeratorName}}){
                    {{this.CounterCall("IncrementEnumerationLoopUsed")}}
                    while({{secondEnumeratorName}}.MoveNext()){
                        var {{secondValueName}}={{secondEnumeratorName}}.Current;
                        {{secondConsumeStatement}}
                    }
                    }
                }
            }
            """;
    }
}
