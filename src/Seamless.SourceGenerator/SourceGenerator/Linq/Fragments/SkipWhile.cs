namespace Seamless.SourceGenerator.Linq.Fragments;
public class SkipWhile_predicate(int 番号, Enumerable source, FuncFragment predicate) : Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        !(method.Parameters.Length > 0 &&
          method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 });
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(predicate,nameof(predicate));
    public override string GetEnumerableExpression =>
        $"global::Seamless.Carrier.ValueEnumerableExtensions.SkipWhile({source.GetEnumerableExpression}, {predicate.DelegateConstructorArg ?? predicate.RenderAsLambda()})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(predicate)}";
    public override string GetEnumeratorFields => "private bool skipping = true;";
    public override string GetTryGetNextBody {
        get {
            var (preCode, condExpr) =this.FuncInvokeCondition(predicate, nameof(predicate),this.temp,this.item);
            var predicateCheck = string.IsNullOrEmpty(preCode)
                ? $"if(this.skipping && {WrapConditionExpression(condExpr)})"
                : $"{preCode}\n            if(this.skipping && {WrapConditionExpression(condExpr)})";

            return $$"""
                while({{this.e}}.MoveNext()) {
                    var {{this.item}} = {{this.e}}.Current;
                    {{predicateCheck}} {
                        continue;
                    }
                    this.skipping = false;
                    current = this.{{this.currentValue}} = {{this.item}};
                    return true;
                }
                current = default!;
                return false;
                """;
        }
    }
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => source.ElementType;
}

public class SkipWhile_indexPredicate(int 番号, Enumerable source, FuncFragment predicate) : SkipWhile_predicate(番号, source, predicate) {
    public static new bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length > 0 &&
        method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 };
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(predicate)}";
    public override string GetEnumeratorFields => $$"""
        private bool skipping = true;
        private int {{this.a}} = -1;
        """;
    public override string GetTryGetNextBody {
        get {
            var (preCode, condExpr) =this.FuncInvokeCondition(predicate, nameof(predicate),this.temp,this.item,this.a);
            var predicateCheck = string.IsNullOrEmpty(preCode)
                ? $"if(this.skipping && {WrapConditionExpression(condExpr)})"
                : $"{preCode}\n            if(this.skipping && {WrapConditionExpression(condExpr)})";

            return $$"""
                while({{this.e}}.MoveNext()) {
                    var {{this.item}} = {{this.e}}.Current;
                    this.{{this.a}}++;
                    {{predicateCheck}} {
                        continue;
                    }
                    this.skipping = false;
                    current = this.{{this.currentValue}} = {{this.item}};
                    return true;
                }
                current = default!;
                return false;
                """;
        }
    }
}
