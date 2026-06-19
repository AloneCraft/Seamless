namespace Tsavorite.SourceGenerator.Linq.Fragments;

public class SkipLast(int 番号, Enumerable source, string skipCount) : Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length==1 && method.Parameters[0].Type.SpecialType==SpecialType.System_Int32;
    private Enumerable Source => source;
    private string SkipCountExpression => skipCount;
    private Enumerable RootSource => this.Source is SkipLast skipLast ? skipLast.RootSource : this.Source;
    private string EffectiveSkipCount => this.Source is SkipLast skipLast
        ? $"(int)global::System.Math.Min(int.MaxValue,(long)({skipLast.EffectiveSkipCount})+({this.SkipCountExpression}))"
        : this.SkipCountExpression;
    protected override Enumerable PrimarySource => this.RootSource;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{this.RootSource.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({this.RootSource.GetEnumeratorExpression}, {this.EffectiveSkipCount})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}, int count";
    public override string GetEnumeratorFields => this.RootSource.CanProvideSpan
        ? $$"""
            private int {{this.a}};
            private int {{this.temp}}=-1;
            """
        : $$"""
            private global::System.Collections.Generic.Queue<{{this.RootSource.ElementType}}>? {{this.lookup}};
            private bool {{this.yielded}};
            """;
    public override string GetTryGetNextBody => this.RootSource.CanProvideSpan
        ? $$"""
            if(this.{{this.temp}}<0) {
                {{this.e}}.TryGetSpan(out var __span0);
                this.{{this.temp}}=__span0.Length-global::System.Math.Min(count<0?0:count,__span0.Length);
            }
            {{this.e}}.TryGetSpan(out var __span);
            if((uint)this.{{this.a}}<(uint)this.{{this.temp}}) {
                current=this.{{this.currentValue}}=__span[this.{{this.a}}++];
                return true;
            }
            current=default!;
            return false;
            """
        : $$"""
            var __c=count<0?0:count;
            if(__c==0) {
                if({{this.e}}.MoveNext()) {
                    current=this.{{this.currentValue}}={{this.e}}.Current;
                    return true;
                }
                current=default!;
                return false;
            }
            if(!this.{{this.yielded}}) {
                this.{{this.lookup}}=new global::System.Collections.Generic.Queue<{{this.RootSource.ElementType}}>(__c+1);
                while(this.{{this.lookup}}.Count<__c&&{{this.e}}.MoveNext()) this.{{this.lookup}}.Enqueue({{this.e}}.Current);
                this.{{this.yielded}}=true;
            }
            if(this.{{this.lookup}} is not null&&{{this.e}}.MoveNext()) {
                this.{{this.lookup}}.Enqueue({{this.e}}.Current);
                current=this.{{this.currentValue}}=this.{{this.lookup}}.Dequeue();
                return true;
            }
            current=default!;
            return false;
            """;
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => this.RootSource.ElementType;
}
