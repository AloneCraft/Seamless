namespace Seamless.SourceGenerator.Linq.Fragments;

public class TakeLast(int 番号, Enumerable source, string takeCount) : Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length==1 && method.Parameters[0].Type.SpecialType==SpecialType.System_Int32;
    private Enumerable Source => source;
    private string TakeCountExpression => takeCount;
    private Enumerable RootSource => Source is TakeLast takeLast ? takeLast.RootSource : Source;
    private string EffectiveTakeCount => Source is TakeLast takeLast
        ? $"global::System.Math.Min({takeLast.EffectiveTakeCount},{TakeCountExpression})"
        : TakeCountExpression;
    protected override Enumerable PrimarySource => RootSource;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{RootSource.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({RootSource.GetEnumeratorExpression}, {EffectiveTakeCount})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}, int count";
    public override string GetEnumeratorFields => RootSource.CanProvideSpan
        ? $$"""
            private int {{a}}=-1;
            private int {{temp}};
            """
        : $$"""
            private global::System.Collections.Generic.Queue<{{RootSource.ElementType}}>? {{lookup}};
            """;
    public override string GetTryGetNextBody => RootSource.CanProvideSpan
        ? $$"""
            {{e}}.TryGetSpan(out var __span);
            if(this.{{a}}<0) {
                var __c=count<0?0:count;
                this.{{a}}=__span.Length-global::System.Math.Min(__c,__span.Length);
                this.{{temp}}=__span.Length;
            }
            if((uint)this.{{a}}<(uint)this.{{temp}}) {
                current=this.{{currentValue}}=__span[this.{{a}}++];
                return true;
            }
            current=default!;
            return false;
            """
        : $$"""
            var __c=count<0?0:count;
            if(this.{{lookup}} is null) {
                this.{{lookup}}=new global::System.Collections.Generic.Queue<{{RootSource.ElementType}}>(__c);
                if(__c==0) {
                    current=default!;
                    return false;
                }
                while({{e}}.MoveNext()) {
                    if(this.{{lookup}}.Count==__c) this.{{lookup}}.Dequeue();
                    this.{{lookup}}.Enqueue({{e}}.Current);
                }
            }
            if(this.{{lookup}}.Count>0) {
                current=this.{{currentValue}}=this.{{lookup}}.Dequeue();
                return true;
            }
            current=default!;
            return false;
            """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => RootSource.ElementType;
}
