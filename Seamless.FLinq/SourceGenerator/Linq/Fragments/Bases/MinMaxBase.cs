namespace Seamless.FLinq.Linq.Fragments;

/// <summary>Shared scalar implementation for Min and Max without a selector.</summary>
public abstract class MinMaxBase(int number, Enumerable source, string resultType, string direction) : BufferedScalar(number, resultType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => $"Compute({this.e})";
    protected override string ComputeMembers => direction=="greater"
        ? $"private static {resultType} Compute(TEnum source) => global::Seamless.System.Carrier.ValueEnumerableExtensions.Max(new global::Seamless.System.Carrier.ValueEnumerable<TEnum, {source.ElementType}>(source));"
        : $"private static {resultType} Compute(TEnum source) => global::Seamless.System.Carrier.ValueEnumerableExtensions.Min(new global::Seamless.System.Carrier.ValueEnumerable<TEnum, {source.ElementType}>(source));";
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";

    public override string? RenderInlineComputation(string destVar,string destType) {
        if(source is RangeRoot range && source.ElementType=="int") {
            return RangePredicateOptimizer.RenderMinMax(range,number,destVar,destType,direction=="greater");
        }
        if(source.DirectArrayExpression is { } arrayExpression) {
            var methodName=direction=="greater"?"MaxSpan":"MinSpan";
            return $$"""
                {{destType}} {{destVar}}=global::Seamless.System.Carrier.ValueEnumerableExtensions.{{methodName}}<{{source.ElementType}}>({{arrayExpression}},global::System.Collections.Generic.Comparer<{{source.ElementType}}>.Default);
                """;
        }
        return this.RenderComputeInline(destVar,destType);
    }
}
