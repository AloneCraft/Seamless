namespace Seamless.SourceGenerator.Linq.Fragments;

public class Intersect(int 番号, Enumerable source, Enumerable second) : Intersect_comparer(番号, source, second, "null") { }

public class Intersect_comparer(int 番号, Enumerable source, Enumerable second, string comparer) : Enumerable(番号) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    public override string GetEnumerableExpression =>
        $"global::Seamless.Linq.Carrier.ValueEnumerableExtensions.Intersect({source.GetEnumerableExpression}, {second.GetEnumerableExpression}, {comparer})";
    protected override string StructTypeParameters => "<TEnum1, TEnum2>";
    protected override string StructTypeConstraints =>
        $"where TEnum1 : struct, global::System.Collections.Generic.IEnumerator<{source.ElementType}>\n    where TEnum2 : struct, global::System.Collections.Generic.IEnumerator<{second.ElementType}>";
    public override string EnumeratorTypeName => $"{Struct}<{source.EnumeratorTypeName}, {second.EnumeratorTypeName}>";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{second.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {second.GetEnumeratorExpression}, {comparer})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum1 source, TEnum2 second, global::System.Collections.Generic.IEqualityComparer<{source.ElementType}>? comparer";
    public override string GetEnumeratorFields => $$"""
        private readonly global::System.Collections.Generic.HashSet<{{source.ElementType}}> {{lookup}}=new(comparer);
        private bool {{yielded}};
        """;
    public override string GetTryGetNextBody => $$"""
        if(!this.{{yielded}}) {
            while(second.MoveNext()) this.{{lookup}}.Add(({{source.ElementType}})(object)second.Current!);
            this.{{yielded}}=true;
        }
        while(source.MoveNext()) {
            var {{item}}=source.Current;
            if(this.{{lookup}}.Remove({{item}})) {
                current=this.{{currentValue}}={{item}};
                return true;
            }
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => source.ElementType;
    protected override string GetEnumeratorDisposeBody => "source.Dispose();\nsecond.Dispose();";
}
