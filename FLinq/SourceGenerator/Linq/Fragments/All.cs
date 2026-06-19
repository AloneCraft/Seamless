namespace Tsavorite.SourceGenerator.Linq.Fragments;
public class All_predicate(int 番号, Enumerable source, FuncFragment predicate) : BufferedScalar(番号, "bool") {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(predicate,nameof(predicate));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(predicate)}";
    protected override string ComputeExpression =>
        $"Compute({this.e}{this.ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorConstructor =>
        $"this.Current = Compute({this.e}{this.ClosureArgsSuffixForCtorBody(predicate)});";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        source is RangeRoot range
            ? RangePredicateOptimizer.RenderAll(range,predicate,番号,destVar,destType)
                ?? (TryRenderDirectLambda(predicate) is { } rangeLambda
                    ? this.RenderDirectEnumerableCallInline(source,destVar,destType,"All",rangeLambda)
                    : this.RenderComputeInline(destVar,destType))
        : TryRenderDirectLambda(predicate) is { } lambda
            ? this.RenderDirectEnumerableCallInline(source,destVar,destType,"All",lambda)
            : this.RenderComputeInline(destVar,destType);
    protected override string ComputeMembers {
        get {
            var (preCode, condExpr) = this.FuncInvokeConditionStatic(predicate, nameof(predicate), this.temp, this.item);
            var predicateCheck = string.IsNullOrEmpty(preCode)
                ? $"if(!{WrapConditionExpression(condExpr)})"
                : $"{preCode}\n        if(!{WrapConditionExpression(condExpr)})";

            return source.CanProvideSpan
                ? $$"""
                    {{AggressiveInliningAttribute}}
                    private static bool Compute(TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}) {
                        source.TryGetSpan(out var __span);
                        foreach(var {{this.item}} in __span){
                            {{predicateCheck}} return false;
                        }
                        return true;
                    }
                    """
                : $$"""
                    {{AggressiveInliningAttribute}}
                    private static bool Compute(TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}) {
                        while(source.MoveNext()) {
                            var {{this.item}}=source.Current;
                            {{predicateCheck}} return false;
                        }
                        return true;
                    }
                    """;
        }
    }
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
