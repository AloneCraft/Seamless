namespace Seamless.SourceGenerator.Linq.Fragments;

public class Reverse(int 番号, Enumerable source) : Enumerable(番号) {
    private Enumerable Source => source;
    private Enumerable RootSource => Source is Reverse reverse ? reverse.RootSource : Source;
    private bool IsIdentity => Source is Reverse reverse && !reverse.IsIdentity;
    protected override Enumerable PrimarySource => RootSource;
    public override string StructMethod => string.Empty;
    public override string GetEnumerableExpression =>
        IsIdentity
            ? RootSource.GetEnumerableExpression
            : $"global::Seamless.Linq.Carrier.ValueEnumerableExtensions.Reverse({RootSource.GetEnumerableExpression})";
    public override string GetEnumeratorStruct => $$"""
        {{RootSource.GetEnumeratorStruct}}
        {{(IsIdentity ? string.Empty : base.GetEnumeratorStruct)}}
        """;
    public override string GetEnumeratorExpression => IsIdentity
        ? RootSource.GetEnumeratorExpression
        : $"new {EnumeratorTypeName}({RootSource.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}";
    public override string GetEnumeratorFields => $$"""
        private int {{a}}=-1;
        private global::System.Collections.Generic.List<{{RootSource.ElementType}}>? {{group}};
        """;
    public override string GetTryGetNextBody => RootSource.CanProvideSpan
        ? $$"""
            {{e}}.TryGetSpan(out var __span);
            if(this.{{a}}<0) this.{{a}}=__span.Length-1;
            if(this.{{a}}>=0) {
                current=this.{{currentValue}}=__span[this.{{a}}--];
                return true;
            }
            current=default!;
            return false;
            """
        : $$"""
            if(this.{{group}} is null) {
                if({{e}}.TryGetNonEnumeratedCount(out var __count)) {
                    this.{{group}}=new global::System.Collections.Generic.List<{{RootSource.ElementType}}>(__count);
                }else{
                    this.{{group}}=new global::System.Collections.Generic.List<{{RootSource.ElementType}}>();
                }
                while({{e}}.MoveNext()) this.{{group}}.Add({{e}}.Current);
                this.{{a}}=this.{{group}}.Count-1;
            }
            if(this.{{a}}>=0) {
                current=this.{{currentValue}}=this.{{group}}[this.{{a}}--];
                return true;
            }
            current=default!;
            return false;
            """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => RootSource.ElementType;
}
