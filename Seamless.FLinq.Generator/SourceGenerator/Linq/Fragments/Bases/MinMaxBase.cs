namespace Seamless.FLinq.Generator;

/// <summary>Shared scalar implementation for Min and Max without a selector.</summary>
internal abstract class MinMaxBase(int number,Enumerable source,string resultType,string direction):BufferedScalar(number,resultType) {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
    }

    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.EnumeratorTypeName,source.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => $"Compute({this.e})";
    protected override string ComputeMembers => direction=="greater"
        ? $"private static {resultType} Compute(TEnum source) => global::Seamless.Linq.Enumerable.Max(new global::Seamless.Linq.ValueEnumerable<TEnum, {source.ElementType}>(source));"
        : $"private static {resultType} Compute(TEnum source) => global::Seamless.Linq.Enumerable.Min(new global::Seamless.Linq.ValueEnumerable<TEnum, {source.ElementType}>(source));";
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";

    public override string? RenderInlineComputation(string destVar,string destType) {
        if(source is RangeRoot range&&source.ElementType=="int") {
            return RangePredicateOptimizer.RenderMinMax(range,number,destVar,destType,direction=="greater");
        }
        var methodName = direction=="greater" ? "MaxSpan" : "MinSpan";
        if(source.DirectArrayExpression is { } arrayExpression) {
            return $$"""
                {{destType}} {{destVar}}=global::Seamless.Linq.Enumerable.{{methodName}}<{{source.ElementType}}>({{arrayExpression}},global::System.Collections.Generic.Comparer<{{source.ElementType}}>.Default);
                """;
        }
        // ArrayRoot 以外(List<T> 等)でも GetDirectSpanExpression を持つ source なら
        // span 経由で MaxSpan/MinSpan(SIMD 実装)に乗せられる。TryGetSpan の実行時プローブを
        // 経由しない DirectSpanRootBase 派生(ListRoot 等)全般に適用される。
        if(this.RenderSpanInline(source,destVar,destType,$"__span{number}",spanName =>
            $"{destVar}=global::Seamless.Linq.Enumerable.{methodName}<{source.ElementType}>({spanName},global::System.Collections.Generic.Comparer<{source.ElementType}>.Default);"
        ) is { } spanResult) {
            return spanResult;
        }
        return this.RenderComputeInline(destVar,destType);
    }
}
