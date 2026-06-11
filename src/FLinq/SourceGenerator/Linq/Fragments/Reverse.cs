namespace Tsavorite.SourceGenerator.Linq.Fragments;

public class Reverse(int 番号, Enumerable source) : Enumerable(番号) {
    private Enumerable Source => source;
    private Enumerable RootSource => this.Source is Reverse reverse ? reverse.RootSource : this.Source;
    private bool IsIdentity => this.Source is Reverse reverse && !reverse.IsIdentity;
    protected override Enumerable PrimarySource => this.RootSource;
    public override string StructMethod => string.Empty;
    public override string GetEnumerableExpression =>
        this.IsIdentity
            ? this.RootSource.GetEnumerableExpression
            : $"global::Tsavorite.Carrier.ValueEnumerableExtensions.Reverse({this.RootSource.GetEnumerableExpression})";
    public override string GetEnumeratorStruct => $$"""
        {{this.RootSource.GetEnumeratorStruct}}
        {{(this.IsIdentity ? string.Empty : base.GetEnumeratorStruct)}}
        """;
    public override string GetEnumeratorExpression => this.IsIdentity
        ? this.RootSource.GetEnumeratorExpression
        : $"new {this.EnumeratorTypeName}({this.RootSource.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    public override string GetEnumeratorFields => $$"""
        private int {{this.a}}=int.MinValue;
        private global::System.Collections.Generic.List<{{this.RootSource.ElementType}}>? {{this.group}};
        """;
    public override string GetTryGetNextBody => this.RootSource.CanProvideSpan
        ? $$"""
            // {{this.a}} は int.MinValue=未初期化 / -1=列挙完了 を区別する。
            // -1 を未初期化扱いにすると、TryGetNext を完了後も呼ぶ消費側（TraversalSupport の while ループ等）で
            // 末尾到達ごとに Length-1 へ再初期化して無限ループになる。
            if(this.{{this.a}}==int.MinValue) {
                {{this.e}}.TryGetSpan(out var __span0);
                this.{{this.a}}=__span0.Length-1;
            }
            if(this.{{this.a}}>=0) {
                {{this.e}}.TryGetSpan(out var __span);
                current=this.{{this.currentValue}}=__span[this.{{this.a}}--];
                return true;
            }
            current=default!;
            return false;
            """
        : $$"""
            if(this.{{this.group}} is null) {
                if({{this.e}}.TryGetNonEnumeratedCount(out var __count)) {
                    this.{{this.group}}=new global::System.Collections.Generic.List<{{this.RootSource.ElementType}}>(__count);
                }else{
                    this.{{this.group}}=new global::System.Collections.Generic.List<{{this.RootSource.ElementType}}>();
                }
                while({{this.e}}.MoveNext()) this.{{this.group}}.Add({{this.e}}.Current);
                this.{{this.a}}=this.{{this.group}}.Count-1;
            }
            if(this.{{this.a}}>=0) {
                current=this.{{this.currentValue}}=this.{{this.group}}[this.{{this.a}}--];
                return true;
            }
            current=default!;
            return false;
            """;
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => this.RootSource.ElementType;
}
