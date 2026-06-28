namespace Seamless.FLinq.Linq.Fragments;

public class TakeLast(int 番号, Enumerable source, string takeCount) : Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length==1 && method.Parameters[0].Type.SpecialType==SpecialType.System_Int32;
    private Enumerable Source => source;
    private string TakeCountExpression => takeCount;
    private Enumerable RootSource => this.Source is TakeLast takeLast ? takeLast.RootSource : this.Source;
    private string EffectiveTakeCount => this.Source is TakeLast takeLast
        ? $"global::System.Math.Min({takeLast.EffectiveTakeCount},{this.TakeCountExpression})"
        : this.TakeCountExpression;
    protected override Enumerable PrimarySource => this.RootSource;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{this.RootSource.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({this.RootSource.GetEnumeratorExpression}, {this.EffectiveTakeCount})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}, int count";
    public override string GetEnumeratorFields => this.RootSource.CanTryProvideSpan
        ? $$"""
            private int {{this.a}}=-1;
            private int {{this.temp}};
            private global::System.Collections.Generic.Queue<{{this.RootSource.ElementType}}>? {{this.lookup}};
            """
        : $$"""
            private global::System.Collections.Generic.Queue<{{this.RootSource.ElementType}}>? {{this.lookup}};
            """;
    public override string GetTryGetNextBody => this.RootSource.CanTryProvideSpan
        ? $$"""
            if({{this.e}}.TryGetSpan(out var __span)) {
                if(this.{{this.a}}<0) {
                    var __spanCount=count<0?0:count;
                    this.{{this.a}}=__span.Length-global::System.Math.Min(__spanCount,__span.Length);
                    this.{{this.temp}}=__span.Length;
                }
                if((uint)this.{{this.a}}<(uint)this.{{this.temp}}) {
                    current=this.{{this.currentValue}}=__span[this.{{this.a}}++];
                    return true;
                }
                current=default!;
                return false;
            }
            var __takeCount=count<0?0:count;
            if(this.{{this.lookup}} is null) {
                this.{{this.lookup}}=new global::System.Collections.Generic.Queue<{{this.RootSource.ElementType}}>(__takeCount);
                if(__takeCount==0) {
                    current=default!;
                    return false;
                }
                while({{this.e}}.MoveNext()) {
                    if(this.{{this.lookup}}.Count==__takeCount) this.{{this.lookup}}.Dequeue();
                    this.{{this.lookup}}.Enqueue({{this.e}}.Current);
                }
            }
            if(this.{{this.lookup}}.Count>0) {
                current=this.{{this.currentValue}}=this.{{this.lookup}}.Dequeue();
                return true;
            }
            current=default!;
            return false;
            """
        : $$"""
            var __c=count<0?0:count;
            if(this.{{this.lookup}} is null) {
                this.{{this.lookup}}=new global::System.Collections.Generic.Queue<{{this.RootSource.ElementType}}>(__c);
                if(__c==0) {
                    current=default!;
                    return false;
                }
                while({{this.e}}.MoveNext()) {
                    if(this.{{this.lookup}}.Count==__c) this.{{this.lookup}}.Dequeue();
                    this.{{this.lookup}}.Enqueue({{this.e}}.Current);
                }
            }
            if(this.{{this.lookup}}.Count>0) {
                current=this.{{this.currentValue}}=this.{{this.lookup}}.Dequeue();
                return true;
            }
            current=default!;
            return false;
            """;
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => this.RootSource.ElementType;
    public override string GetConsumeExecutionStatement(string consumerName) {
        if(this.RootSource is not RangeRoot range ||this.RootSource.ElementType!="int") {
            return base.GetConsumeExecutionStatement(consumerName);
        }

        var start=$"__start{番号}";
        var count=$"__count{番号}";
        var take=$"__take{番号}";
        var actual=$"__actual{番号}";
        var skip=$"__skip{番号}";
        var index=$"__i{番号}";
        return $$"""
            {
                var {{start}}={{range.StartExpression}};
                var {{count}}={{range.CountExpression}};
                var {{take}}={{this.EffectiveTakeCount}};
                var {{actual}}=global::System.Math.Min({{count}},global::System.Math.Max(0,{{take}}));
                var {{skip}}={{count}} - {{actual}};
                for(var {{index}}=0;{{index}}<{{actual}};{{index}}++) {
                    {{consumerName}}.Consume({{start}} + {{skip}} + {{index}});
                }
            }
            """;
    }
}
