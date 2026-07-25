namespace Seamless.FLinq.Linq.Fragments;

public class Index(int 番号,Enumerable source):Enumerable(番号) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    public override string GetEnumerableExpression =>
        $"global::Seamless.Linq.Enumerable.Index({source.GetEnumerableExpression})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    public override string GetEnumeratorFields => $"private int {this.a}=0;";
    public override string GetTryGetNextBody => $$"""
        if({{this.e}}.TryGetNext(out var __mn37)) {
            current=this.{{this.currentValue}}=(this.{{this.a}}++,__mn37);
            return true;
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => $"(int Index, {source.ElementType} Item)";
    public override string GetConsumeExecutionStatement(string consumerName) {
        var sourceName = $"__indexSource{番号}";
        var directSpanExpression = source.GetDirectSpanExpression(sourceName);
        if(directSpanExpression is null) {
            return base.GetConsumeExecutionStatement(consumerName);
        }
        var spanName = $"__indexSpan{番号}";
        return $$"""
            using var {{sourceName}}={{source.GetEnumeratorExpression}};
            global::Seamless.Linq.LinqCounterScope.IncrementDirectSpanUsed();
            var {{spanName}}={{directSpanExpression}};
            for(var __i{{番号}}=0;__i{{番号}}<{{spanName}}.Length;__i{{番号}}++) {
                {{consumerName}}.Consume((__i{{番号}},{{spanName}}[__i{{番号}}]));
            }
            """;
    }
}
