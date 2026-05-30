namespace Seamless.SourceGenerator.Linq.Fragments;
public class All(int 番号, Enumerable source, FuncFragment predicate) : Scalar(番号) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(predicate,nameof(predicate));
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{DefineScalarEnumeratorStruct("bool")}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(predicate)}";
    public override string GetEnumeratorFields => $$"""
        private bool {{currentValue}} = true;
        private bool {{yielded}} = false;
        """;
    public override string GetTryGetNextBody {
        get {
            var (preCode, condExpr) = FuncInvokeCondition(predicate, nameof(predicate), temp, item);
            var predicateCheck = string.IsNullOrEmpty(preCode)
                ? $"if(!({condExpr}))"
                : $"{preCode}\n            if(!{condExpr})";

            return $$"""
                if(this.{{yielded}}) {
                    current = default;
                    return false;
                }
                this.{{yielded}} = true;
                while({{e}}.MoveNext()) {
                    var {{item}} = {{e}}.Current;
                    {{predicateCheck}} {
                        this.{{currentValue}} = false;
                        current = this.{{currentValue}};
                        return true;
                    }
                }
                this.{{currentValue}} = true;
                current = this.{{currentValue}};
                return true;
                """;
        }
    }
    public override string GetCurrentExpression => currentValue;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
