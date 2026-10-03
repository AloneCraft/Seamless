namespace Seamless.FLinq.Generator;

internal class Chunk(int 番号,Enumerable source,string size):Enumerable(番号) {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
    }

    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.EnumeratorTypeName,source.GetEnumeratorExpression,size);
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}, int size";
    // yielded は「source を使い切った」旗。端数の最後のチャンクを返したあとの呼び出しで source をもう一度読まないために使う
    // (Tsavorite の列挙子のように、使い切ったあとの MoveNext で最初から読み直すものがあり、チャンクが無限に出続ける)。
    public override string GetEnumeratorFields => source.CanTryProvideSpan
        ? $$"""
            private int {{this.a}};
            private {{source.ElementType}}[]? {{this.group}};
            private bool {{this.yielded}};
            """
        : $$"""
            private {{source.ElementType}}[]? {{this.group}};
            private bool {{this.yielded}};
            """;
    public override string GetTryGetNextBody => source.CanTryProvideSpan
        ? $$"""
            if(size<=0) throw new global::System.ArgumentOutOfRangeException(nameof(size));
            if({{this.e}}.TryGetSpan(out var __span)){
                if((uint)this.{{this.a}}<(uint)__span.Length){
                    var len=global::System.Math.Min(size,__span.Length-this.{{this.a}});
                    current=this.{{this.currentValue}}=__span.Slice(this.{{this.a}},len).ToArray();
                    this.{{this.a}}+=len;
                    return true;
                }
                current=default!;
                return false;
            }
            if(this.{{this.yielded}}){
                current=default!;
                return false;
            }
            var __i=0;
            if(this.{{this.group}} is null) this.{{this.group}}=new {{source.ElementType}}[size];
            while({{this.e}}.TryGetNext(out var __mn14)){
                this.{{this.group}}[__i++]= __mn14;
                if(__i==size){
                    current=this.{{this.currentValue}}=this.{{this.group}};
                    this.{{this.group}}=null;
                    return true;
                }
            }
            this.{{this.yielded}}=true;
            if(__i>0){
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
            if(this.{{this.yielded}}){
                current=default!;
                return false;
            }
            var __i=0;
            if(this.{{this.group}} is null) this.{{this.group}}=new {{source.ElementType}}[size];
            while({{this.e}}.TryGetNext(out var __mn15)){
                this.{{this.group}}[__i++]= __mn15;
                if(__i==size){
                    current=this.{{this.currentValue}}=this.{{this.group}};
                    this.{{this.group}}=null;
                    return true;
                }
            }
            this.{{this.yielded}}=true;
            if(__i>0){
                var __last=new {{source.ElementType}}[__i];
                global::System.Array.Copy(this.{{this.group}},__last,__i);
                current=this.{{this.currentValue}}=__last;
                this.{{this.group}}=null;
                return true;
            }
            current=default!;
            return false;
            """;
    public override string ElementType => $"{source.ElementType}[]";
    public override string GetConsumeExecutionStatement(string consumerName) =>
        this.RenderRangeChunkConsume(consumerName)
        ??this.RenderDirectSpanChunkConsume(consumerName)
        ??base.GetConsumeExecutionStatement(consumerName);
    private string? RenderDirectSpanChunkConsume(string consumerName) {
        var sourceName = $"__chunkSource{番号}";
        var directSpanExpression = source.GetDirectSpanExpression(sourceName);
        if(directSpanExpression is null) {
            return null;
        }

        var chunkSize = $"__chunkSize{番号}";
        var spanName = $"__chunkSpan{番号}";
        var offset = $"__offset{番号}";
        var length = $"__length{番号}";
        return $$"""
            using var {{sourceName}}={{source.GetEnumeratorExpression}};
            global::Seamless.Linq.LinqCounterScope.IncrementDirectSpanUsed();
            var {{spanName}}={{directSpanExpression}};
            var {{chunkSize}}={{size}};
            if({{chunkSize}}<=0) throw new global::System.ArgumentOutOfRangeException("size");
            for(var {{offset}}=0;{{offset}}<{{spanName}}.Length;){
                var {{length}}=global::System.Math.Min({{chunkSize}},{{spanName}}.Length-{{offset}});
                {{consumerName}}.Consume({{spanName}}.Slice({{offset}},{{length}}).ToArray());
                {{offset}}+={{length}};
            }
            """;
    }
    private string? RenderRangeChunkConsume(string consumerName) {
        if(source is not RangeRoot range||source.ElementType!="int") {
            return null;
        }

        var chunkSize = $"__chunkSize{番号}";
        var start = $"__start{番号}";
        var count = $"__count{番号}";
        var offset = $"__offset{番号}";
        var length = $"__length{番号}";
        var chunk = $"__chunk{番号}";
        var index = $"__j{番号}";
        return $$"""
            {
                var {{chunkSize}}={{size}};
                if({{chunkSize}}<=0) throw new global::System.ArgumentOutOfRangeException("size");
                {{range.RenderArgumentPreparation(start,count)}}
                for(var {{offset}}=0;{{offset}}<{{count}};){
                    var {{length}}=global::System.Math.Min({{chunkSize}},{{count}}-{{offset}});
                    var {{chunk}}=new int[{{length}}];
                    for(var {{index}}=0;{{index}}<{{length}};{{index}}++){
                        {{chunk}}[{{index}}]={{start}} + {{offset}} + {{index}};
                    }
                    {{offset}}+={{length}};
                    {{consumerName}}.Consume({{chunk}});
                }
            }
            """;
    }
}
