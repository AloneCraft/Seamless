namespace Seamless.FLinq.Linq.Fragments;

public class SkipLast(int 番号,Enumerable source,string skipCount):Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length==1&&method.Parameters[0].Type.SpecialType==SpecialType.System_Int32;
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
    public override string GetEnumeratorFields => this.RootSource.CanTryProvideSpan
        ? $$"""
            private int {{this.a}};
            private int {{this.temp}}=-1;
            private global::System.Collections.Generic.Queue<{{this.RootSource.ElementType}}>? {{this.lookup}};
            private bool {{this.yielded}};
            """
        : $$"""
            private global::System.Collections.Generic.Queue<{{this.RootSource.ElementType}}>? {{this.lookup}};
            private bool {{this.yielded}};
            """;
    public override string GetTryGetNextBody => this.RootSource.CanTryProvideSpan
        ? $$"""
            if({{this.e}}.TryGetSpan(out var __span)) {
                if(this.{{this.temp}}<0) {
                    this.{{this.temp}}=__span.Length-global::System.Math.Min(count<0?0:count,__span.Length);
                }
                if((uint)this.{{this.a}}<(uint)this.{{this.temp}}) {
                    current=this.{{this.currentValue}}=__span[this.{{this.a}}++];
                    return true;
                }
                current=default!;
                return false;
            }
            var __c=count<0?0:count;
            if(__c==0) {
                if({{this.e}}.TryGetNext(out var __mn63)) {
                    current=this.{{this.currentValue}}=__mn63;
                    return true;
                }
                current=default!;
                return false;
            }
            if(!this.{{this.yielded}}) {
                this.{{this.lookup}}=new global::System.Collections.Generic.Queue<{{this.RootSource.ElementType}}>(__c+1);
                while(this.{{this.lookup}}.Count<__c&&{{this.e}}.TryGetNext(out var __mn64)) this.{{this.lookup}}.Enqueue(__mn64);
                this.{{this.yielded}}=true;
            }
            if(this.{{this.lookup}} is not null&&{{this.e}}.TryGetNext(out var __mn65)) {
                this.{{this.lookup}}.Enqueue(__mn65);
                current=this.{{this.currentValue}}=this.{{this.lookup}}.Dequeue();
                return true;
            }
            current=default!;
            return false;
            """
        : $$"""
            var __c=count<0?0:count;
            if(__c==0) {
                if({{this.e}}.TryGetNext(out var __mn66)) {
                    current=this.{{this.currentValue}}=__mn66;
                    return true;
                }
                current=default!;
                return false;
            }
            if(!this.{{this.yielded}}) {
                this.{{this.lookup}}=new global::System.Collections.Generic.Queue<{{this.RootSource.ElementType}}>(__c+1);
                while(this.{{this.lookup}}.Count<__c&&{{this.e}}.TryGetNext(out var __mn67)) this.{{this.lookup}}.Enqueue(__mn67);
                this.{{this.yielded}}=true;
            }
            if(this.{{this.lookup}} is not null&&{{this.e}}.TryGetNext(out var __mn68)) {
                this.{{this.lookup}}.Enqueue(__mn68);
                current=this.{{this.currentValue}}=this.{{this.lookup}}.Dequeue();
                return true;
            }
            current=default!;
            return false;
            """;
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => this.RootSource.ElementType;
    public override string GetConsumeExecutionStatement(string consumerName) {
        var sourceName = $"__skipLastSource{番号}";
        var spanExpression = this.RootSource.GetDirectSpanExpression(sourceName);
        if(spanExpression is null) {
            return base.GetConsumeExecutionStatement(consumerName);
        }
        return $$"""
            using var {{sourceName}}={{this.RootSource.GetEnumeratorExpression}};
            var __skipLastSpan{{番号}}={{spanExpression}};
            var __skipLastCount{{番号}}=global::System.Math.Clamp({{this.EffectiveSkipCount}},0,__skipLastSpan{{番号}}.Length);
            var __skipLastLength{{番号}}=__skipLastSpan{{番号}}.Length-__skipLastCount{{番号}};
            for(var __skipLastIndex{{番号}}=0;__skipLastIndex{{番号}}<__skipLastLength{{番号}};__skipLastIndex{{番号}}++){
                {{consumerName}}.Consume(__skipLastSpan{{番号}}[__skipLastIndex{{番号}}]);
            }
            """;
    }
}
