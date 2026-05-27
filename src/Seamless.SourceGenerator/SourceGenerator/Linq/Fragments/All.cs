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
    public override string GetTryGetNextBody => $$"""
        if({{yielded}}) {
            current = default;
            return false;
        }
        {{yielded}} = true;
        while({{e}}.MoveNext()) {
            var {{item}} = {{e}}.Current;
            bool {{temp}} = default!;
            {{FuncAssign(predicate,nameof(predicate),temp,item)}}
            if(!{{temp}}) {
                {{currentValue}} = false;
                current = {{currentValue}};
                return true;
            }
        }
        {{currentValue}} = true;
        current = {{currentValue}};
        return true;
        """;
    public override string GetCurrentExpression => currentValue;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
