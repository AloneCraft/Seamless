namespace Seamless.SourceGenerator.Linq.Fragments;
public class All(int 番号, Enumerable source, FuncFragment predicate) : Scalar(番号) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(predicate,nameof(predicate));
    public override string GetEnumeratorExpression =>
        NeedsStructClosure(predicate)
            ? $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {this.ClosureArgsSuffix(predicate)})"
            : predicate.CanInlineAssign && (predicate.CanGenerateMethod || predicate.RequiresStatementInlining)
            ? $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})"
            : $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {this.FuncDelegate(predicate,nameof(predicate))})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{this.DefineScalarEnumeratorStruct("bool")}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters =>
        NeedsStructClosure(predicate)
            ? $"TEnum {this.e}, {this.ClosurePrimaryCtorSuffix(predicate)}"
            : predicate.CanInlineAssign && (predicate.CanGenerateMethod || predicate.RequiresStatementInlining)
            ? $"TEnum {this.e}"
            : $"TEnum {this.e}, global::System.Func<{source.ElementType}, bool> predicate";
    public override string GetEnumeratorFields => $$"""
        private bool {{this.currentValue}} = true;
        private bool {{this.yielded}} = false;
        """;
    public override string GetTryGetNextBody => $$"""
        if({{this.yielded}}) {
            current = default;
            return false;
        }
        {{this.yielded}} = true;
        while({{this.e}}.MoveNext()) {
            var {{this.item}} = {{this.e}}.Current;
            bool {{this.temp}} = default!;
            {{this.FuncAssign(predicate,nameof(predicate),this.temp,this.item)}}
            if(!{{this.temp}}) {
                {{this.currentValue}} = false;
                current = {{this.currentValue}};
                return true;
            }
        }
        {{this.currentValue}} = true;
        current = {{this.currentValue}};
        return true;
        """;
    public override string GetCurrentExpression => this.currentValue;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
