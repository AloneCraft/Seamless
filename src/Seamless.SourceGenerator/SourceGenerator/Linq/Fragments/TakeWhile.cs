namespace Seamless.SourceGenerator.Linq.Fragments;

public class TakeWhile_predicate(int 番号, Enumerable source, FuncFragment predicate) : Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        !(method.Parameters.Length > 0 &&
          method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 });
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(predicate,nameof(predicate));
    public override string GetEnumerableExpression =>
        $"global::Seamless.Linq.Carrier.ValueEnumerableExtensions.TakeWhile({source.GetEnumerableExpression}, {predicate.DelegateConstructorArg ?? predicate.RenderAsLambda()})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(predicate)}";
    public override string GetEnumeratorFields => "private bool done = false;";
    public override string GetTryGetNextBody {
        get {
            var (preCode, condExpr) = FuncInvokeCondition(predicate, nameof(predicate), temp, item);
            var predicateCheck = string.IsNullOrEmpty(preCode)
                ? $"if(!{WrapConditionExpression(condExpr)})"
                : $"{preCode}\n        if(!{condExpr})";

            return $$"""
                if(this.done || !{{e}}.MoveNext()) {
                    current = default!;
                    return false;
                }
                var {{item}} = {{e}}.Current;
                {{predicateCheck}} {
                    this.done = true;
                    current = default!;
                    return false;
                }
                current = this.{{currentValue}} = {{item}};
                return true;
                """;
        }
    }
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => source.ElementType;
}

public class TakeWhile_indexPredicate(int 番号, Enumerable source, FuncFragment predicate) : TakeWhile_predicate(番号, source, predicate) {
    public static new bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length > 0 &&
        method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 };
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(predicate)}";
    public override string GetEnumeratorFields => $$"""
        private bool done = false;
        private int {{a}} = -1;
        """;
    public override string GetTryGetNextBody {
        get {
            var (preCode, condExpr) = FuncInvokeCondition(predicate, nameof(predicate), temp, item, a);
            var predicateCheck = string.IsNullOrEmpty(preCode)
                ? $"if(!{WrapConditionExpression(condExpr)})"
                : $"{preCode}\n        if(!{condExpr})";

            return $$"""
                if(this.done || !{{e}}.MoveNext()) {
                    current = default!;
                    return false;
                }
                var {{item}} = {{e}}.Current;
                this.{{a}}++;
                {{predicateCheck}} {
                    this.done = true;
                    current = default!;
                    return false;
                }
                current = this.{{currentValue}} = {{item}};
                return true;
                """;
        }
    }
}
