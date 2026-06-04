namespace Seamless.SourceGenerator.Linq.Fragments;

public class DistinctBy(int 番号, Enumerable source, FuncFragment keySelector) : DistinctBy_comparer(番号, source, keySelector, "null") { }

public class DistinctBy_comparer(int 番号, Enumerable source, FuncFragment keySelector, string comparer) : Enumerable(番号) {
    private string KeySelectorFuncType => $"global::System.Func<{source.ElementType}, {FuncReturnTypeName(keySelector)}>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(keySelector,nameof(keySelector));
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {comparer}{ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}, global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(keySelector)}>? comparer{ClosurePrimaryCtorSuffix(keySelector)}";
    public override string GetEnumeratorFields => $$"""
        private readonly global::System.Collections.Generic.HashSet<{{FuncReturnTypeName(keySelector)}}> {{lookup}}=new(comparer);
        """;
    public override string GetTryGetNextBody => $$"""
        while({{e}}.MoveNext()) {
            var {{item}}={{e}}.Current;
            {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
            {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
            if(this.{{lookup}}.Add({{key}})) {
                current=this.{{currentValue}}={{item}};
                return true;
            }
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => source.ElementType;
}
