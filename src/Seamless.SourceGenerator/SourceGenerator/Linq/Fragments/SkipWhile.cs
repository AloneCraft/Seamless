namespace Seamless.SourceGenerator.Linq.Fragments;
public class SkipWhile(int 番号, Enumerable source, FuncFragment predicate) : Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        !(method.Parameters.Length > 0 &&
          method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 });
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(predicate,nameof(predicate));
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(predicate)}";
    public override string GetEnumeratorFields => $$"""
        private bool skipping = true;
        """;
    public override string GetTryGetNextBody {
        get {
            var (preCode, condExpr) = FuncInvokeCondition(predicate, nameof(predicate), temp, item);
            var predicateCheck = string.IsNullOrEmpty(preCode)
                ? $"if(this.skipping && {WrapConditionExpression(condExpr)})"
                : $"{preCode}\n            if(this.skipping && {condExpr})";

            return $$"""
                while({{e}}.MoveNext()) {
                    var {{item}} = {{e}}.Current;
                    {{predicateCheck}} {
                        continue;
                    }
                    this.skipping = false;
                    current = this.{{currentValue}} = {{item}};
                    return true;
                }
                current = default!;
                return false;
                """;
        }
    }
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => source.ElementType;
}

public class SkipWhile_index(int 番号, Enumerable source, FuncFragment predicate) : SkipWhile(番号, source, predicate) {
    public static new bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length > 0 &&
        method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 };
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(predicate)}";
    public override string GetEnumeratorFields => $$"""
        private bool skipping = true;
        private int {{a}} = -1;
        """;
    public override string GetTryGetNextBody {
        get {
            var (preCode, condExpr) = FuncInvokeCondition(predicate, nameof(predicate), temp, item, a);
            var predicateCheck = string.IsNullOrEmpty(preCode)
                ? $"if(this.skipping && {WrapConditionExpression(condExpr)})"
                : $"{preCode}\n            if(this.skipping && {condExpr})";

            return $$"""
                while({{e}}.MoveNext()) {
                    var {{item}} = {{e}}.Current;
                    this.{{a}}++;
                    {{predicateCheck}} {
                        continue;
                    }
                    this.skipping = false;
                    current = this.{{currentValue}} = {{item}};
                    return true;
                }
                current = default!;
                return false;
                """;
        }
    }
}
