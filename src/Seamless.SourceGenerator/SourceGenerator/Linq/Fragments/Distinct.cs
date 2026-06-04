namespace Seamless.SourceGenerator.Linq.Fragments;

public class Distinct(int 番号, Enumerable source) : Distinct_comparer(番号, source, "null") { }

public class Distinct_comparer(int 番号, Enumerable source, string comparer) : Enumerable(番号) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {comparer})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}, global::System.Collections.Generic.IEqualityComparer<{source.ElementType}>? comparer";
    public override string GetEnumeratorFields => $$"""
        private readonly global::System.Collections.Generic.HashSet<{{source.ElementType}}> {{lookup}}=new(comparer);
        """;
    public override string GetTryGetNextBody => $$"""
        while({{e}}.MoveNext()) {
            var {{item}}={{e}}.Current;
            if(this.{{lookup}}.Add({{item}})) {
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
