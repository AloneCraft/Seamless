namespace Seamless.SourceGenerator.Linq.Fragments;
public class All_predicate(int 番号, Enumerable source, FuncFragment predicate) : Scalar(番号) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(predicate,nameof(predicate));
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{this.DefineScalarEnumeratorStruct("bool")}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(predicate)}";
    protected override string GetCurrentPropertyDefaultExpression => "true";
    public override string GetEnumeratorFields => $$"""
        private bool {{this.yielded}} = false;
        """;
    public override string GetTryGetNextBody {
        get {
            var (preCode, condExpr) =this.FuncInvokeCondition(predicate, nameof(predicate),this.temp,this.item);
            var predicateCheck = string.IsNullOrEmpty(preCode)
                ? $"if(!{WrapConditionExpression(condExpr)})"
                : $"{preCode}\n            if(!{condExpr})";

            return source.CanProvideSpan
                ? $$"""
                    if(this.{{this.yielded}}) {
                        current = default;
                        return false;
                    }
                    this.{{this.yielded}} = true;
                    {{this.e}}.TryGetSpan(out var __span);
                    foreach(var {{this.item}} in __span){
                        {{predicateCheck}} {
                            this.{{this.currentValue}}=false;
                            current=this.{{this.currentValue}};
                            return true;
                        }
                    }
                    this.{{this.currentValue}}=true;
                    current=this.{{this.currentValue}};
                    return true;
                    """
                : $$"""
                    if(this.{{this.yielded}}) {
                        current = default;
                        return false;
                    }
                    this.{{this.yielded}} = true;
                    while({{this.e}}.MoveNext()) {
                        var {{this.item}} = {{this.e}}.Current;
                        {{predicateCheck}} {
                            this.{{this.currentValue}} = false;
                            current = this.{{this.currentValue}};
                            return true;
                        }
                    }
                    this.{{this.currentValue}} = true;
                    current = this.{{this.currentValue}};
                    return true;
                    """;
        }
    }
    public override string GetCurrentExpression => this.currentValue;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
