namespace Seamless.SourceGenerator.Linq.Fragments;

public class SkipLast(int 番号, Enumerable source, string skipCount) : Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length==1 && method.Parameters[0].Type.SpecialType==SpecialType.System_Int32;
    private Enumerable Source => source;
    private string SkipCountExpression => skipCount;
    private Enumerable RootSource => Source is SkipLast skipLast ? skipLast.RootSource : Source;
    private string EffectiveSkipCount => Source is SkipLast skipLast
        ? $"(int)global::System.Math.Min(int.MaxValue,(long)({skipLast.EffectiveSkipCount})+({SkipCountExpression}))"
        : SkipCountExpression;
    protected override Enumerable PrimarySource => RootSource;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{RootSource.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({RootSource.GetEnumeratorExpression}, {EffectiveSkipCount})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}, int count";
    public override string GetEnumeratorFields => RootSource.CanProvideSpan
        ? $$"""
            private int {{a}};
            private int {{temp}}=-1;
            """
        : $$"""
            private global::System.Collections.Generic.Queue<{{RootSource.ElementType}}>? {{lookup}};
            private bool {{yielded}};
            """;
    public override string GetTryGetNextBody => RootSource.CanProvideSpan
        ? $$"""
            if(this.{{temp}}<0) {
                {{e}}.TryGetSpan(out var __span0);
                this.{{temp}}=__span0.Length-global::System.Math.Min(count<0?0:count,__span0.Length);
            }
            {{e}}.TryGetSpan(out var __span);
            if((uint)this.{{a}}<(uint)this.{{temp}}) {
                current=this.{{currentValue}}=__span[this.{{a}}++];
                return true;
            }
            current=default!;
            return false;
            """
        : $$"""
            var __c=count<0?0:count;
            if(__c==0) {
                if({{e}}.MoveNext()) {
                    current=this.{{currentValue}}={{e}}.Current;
                    return true;
                }
                current=default!;
                return false;
            }
            if(!this.{{yielded}}) {
                this.{{lookup}}=new global::System.Collections.Generic.Queue<{{RootSource.ElementType}}>(__c+1);
                while(this.{{lookup}}.Count<__c&&{{e}}.MoveNext()) this.{{lookup}}.Enqueue({{e}}.Current);
                this.{{yielded}}=true;
            }
            if(this.{{lookup}} is not null&&{{e}}.MoveNext()) {
                this.{{lookup}}.Enqueue({{e}}.Current);
                current=this.{{currentValue}}=this.{{lookup}}.Dequeue();
                return true;
            }
            current=default!;
            return false;
            """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => RootSource.ElementType;
}
