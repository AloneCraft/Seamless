namespace Seamless.FLinq.Linq.Fragments;

public class Chunk(int 番号, Enumerable source, string size) : Enumerable(番号) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {size})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}, int size";
    public override string GetEnumeratorFields => source.CanTryProvideSpan
        ? $$"""
            private int {{this.a}};
            private {{source.ElementType}}[]? {{this.group}};
            """
        : $$"""
            private {{source.ElementType}}[]? {{this.group}};
            """;
    public override string GetTryGetNextBody => source.CanTryProvideSpan
        ? $$"""
            if(size<=0) throw new global::System.ArgumentOutOfRangeException(nameof(size));
            if({{this.e}}.TryGetSpan(out var __span)) {
                if((uint)this.{{this.a}}<(uint)__span.Length) {
                    var len=global::System.Math.Min(size,__span.Length-this.{{this.a}});
                    current=this.{{this.currentValue}}=__span.Slice(this.{{this.a}},len).ToArray();
                    this.{{this.a}}+=len;
                    return true;
                }
                current=default!;
                return false;
            }
            var __i=0;
            if(this.{{this.group}} is null) this.{{this.group}}=new {{source.ElementType}}[size];
            while({{this.e}}.MoveNext()) {
                this.{{this.group}}[__i++]= {{this.e}}.Current;
                if(__i==size) {
                    current=this.{{this.currentValue}}=this.{{this.group}};
                    this.{{this.group}}=new {{source.ElementType}}[size];
                    return true;
                }
            }
            if(__i>0) {
                var __last=new {{source.ElementType}}[__i];
                global::System.Array.Copy(this.{{this.group}},__last,__i);
                current=this.{{this.currentValue}}=__last;
                this.{{this.group}}=null;
                return true;
            }
            current=default!;
            return false;
            """
        : $$"""
            if(size<=0) throw new global::System.ArgumentOutOfRangeException(nameof(size));
            var __i=0;
            if(this.{{this.group}} is null) this.{{this.group}}=new {{source.ElementType}}[size];
            while({{this.e}}.MoveNext()) {
                this.{{this.group}}[__i++]= {{this.e}}.Current;
                if(__i==size) {
                    current=this.{{this.currentValue}}=this.{{this.group}};
                    this.{{this.group}}=new {{source.ElementType}}[size];
                    return true;
                }
            }
            if(__i>0) {
                var __last=new {{source.ElementType}}[__i];
                global::System.Array.Copy(this.{{this.group}},__last,__i);
                current=this.{{this.currentValue}}=__last;
                this.{{this.group}}=null;
                return true;
            }
            current=default!;
            return false;
            """;
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => $"{source.ElementType}[]";
    public override string GetConsumeExecutionStatement(string consumerName) =>
        this.RenderRangeChunkConsume(consumerName)
        ?? base.GetConsumeExecutionStatement(consumerName);
    private string? RenderRangeChunkConsume(string consumerName) {
        if(source is not RangeRoot range || source.ElementType!="int") {
            return null;
        }

        var chunkSize=$"__chunkSize{番号}";
        var start=$"__start{番号}";
        var count=$"__count{番号}";
        var offset=$"__offset{番号}";
        var length=$"__length{番号}";
        var chunk=$"__chunk{番号}";
        var index=$"__j{番号}";
        return $$"""
            {
                var {{chunkSize}}={{size}};
                if({{chunkSize}}<=0) throw new global::System.ArgumentOutOfRangeException("size");
                var {{start}}={{range.StartExpression}};
                var {{count}}={{range.CountExpression}};
                for(var {{offset}}=0;{{offset}}<{{count}};) {
                    var {{length}}=global::System.Math.Min({{chunkSize}},{{count}}-{{offset}});
                    var {{chunk}}=new int[{{length}}];
                    for(var {{index}}=0;{{index}}<{{length}};{{index}}++) {
                        {{chunk}}[{{index}}]={{start}} + {{offset}} + {{index}};
                    }
                    {{offset}}+={{length}};
                    {{consumerName}}.Consume({{chunk}});
                }
            }
            """;
    }
}
