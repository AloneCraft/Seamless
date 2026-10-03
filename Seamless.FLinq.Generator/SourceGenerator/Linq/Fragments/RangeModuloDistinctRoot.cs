namespace Seamless.FLinq.Generator;

/*
RangeModuloDistinctRoot は Enumerable.Range(start,count).DistinctBy(x => x%C)(C は正の定数)を、集合を使わずに作る。

考え方:
  C 個の連続する整数の剰余はすべて異なり、その先は繰り返す。C# の剰余は被除数の符号を持つので、
  負の値のキーは (-C,0]、0 以上の値のキーは [0,C) で、両者が重なるのはキー 0 だけ。
  - 負の区間 [start,min(end,0)) の初出は先頭の C 個まで(すべて異なるキー)。
  - 0 以上の区間 [max(start,0),end) の初出も先頭の C 個までで、キー 0 は負の区間に C の倍数があれば既に出ている。
  よって、2つの区間の先頭 C 個ずつを順に流し、後半ではキー 0 の値(1つだけ)を必要なら飛ばせばよい。
*/
internal sealed class RangeModuloDistinctRoot(int 番号,RangeRoot range,int modulus):Enumerable(番号) {
    public override string? NonEnumeratedCountExpression => null;
    public override string StructMethod => string.Empty;
    public override string ElementType => "int";
    public override string? DirectEnumerableExpression =>
        $"global::System.Linq.Enumerable.DistinctBy(global::System.Linq.Enumerable.Range({range.StartExpression}, {range.CountExpression}), static __x => __x % {modulus})";
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.Struct,range.StartExpression,range.CountExpression);
    public override string GetEnumeratorPrimaryConstructorParameters => "int start, int count";
    public override string GetEnumeratorFields => string.Empty;
    public override string GetTryGetNextBody => throw new NotSupportedException("RangeModuloDistinctRoot は専用の列挙子構造体を出す。");

    // 負の区間 [start,negativeEnd)、0 以上の区間 [positiveStart,positiveEnd)、後者でキー 0 を飛ばすか。
    private string Intervals(string start,string end,string negativeEnd,string positiveStart,string positiveEnd,string skipZero) => $$"""
        var {{negativeEnd}}=global::System.Math.Min(global::System.Math.Min({{end}},0L),(long){{start}}+{{modulus}});
        var {{positiveStart}}=global::System.Math.Max((long){{start}},0L);
        var {{positiveEnd}}=global::System.Math.Min({{end}},{{positiveStart}}+{{modulus}});
        var {{skipZero}}=(long){{start}}+((-(long){{start}})%{{modulus}}+{{modulus}})%{{modulus}}<{{negativeEnd}};
        """;

    public override string GetEnumeratorStruct => $$"""
        private struct {{this.Struct}} : global::Seamless.Linq.IValueEnumerator<int>
        {
            private long next;
            private readonly long negativeEnd;
            private readonly long positiveStart;
            private readonly long positiveEnd;
            private readonly bool skipZero;
            private bool positive;
            public {{this.Struct}}(int start,int count) {
                // Enumerable.Range と同じ引数検査(列挙子を作る時点で例外にする)。
                if(count<0||(long)start+count-1>int.MaxValue){
                    throw new global::System.ArgumentOutOfRangeException("count");
                }
                var __end=(long)start+count;
                {{GroupBySupport.IndentBlock(this.Intervals("start","__end","__negativeEnd","__positiveStart","__positiveEnd","__skipZero"),"        ").TrimStart()}}
                this.negativeEnd=__negativeEnd;
                this.positiveStart=__positiveStart;
                this.positiveEnd=__positiveEnd;
                this.skipZero=__skipZero;
                this.next=start;
                this.positive=false;
            }
            public int Current{get;private set;}=default!;
            public bool TryGetNonEnumeratedCount(out int count){
                count=0;
                return false;
            }
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) { span = default; return false; }
            public bool TryCopyTo(scoped global::System.Span<int> destination,global::System.Index offset) => false;
            public bool TryGetNext(out int current){
                if(!this.positive){
                    if(this.next<this.negativeEnd){
                        current=this.Current=(int)this.next++;
                        return true;
                    }
                    this.positive=true;
                    this.next=this.positiveStart;
                }
                while(this.next<this.positiveEnd){
                    var __value=this.next++;
                    if(this.skipZero&&__value%{{modulus}}==0){
                        continue;
                    }
                    current=this.Current=(int)__value;
                    return true;
                }
                current=default;
                return false;
            }
            public void Dispose(){}
        }
        """;

    public override string GetConsumeExecutionStatement(string consumerName) {
        var start = $"__start{番号}";
        var count = $"__count{番号}";
        var end = $"__end{番号}";
        var negativeEnd = $"__negativeEnd{番号}";
        var positiveStart = $"__positiveStart{番号}";
        var positiveEnd = $"__positiveEnd{番号}";
        var skipZero = $"__skipZero{番号}";
        var value = $"__value{番号}";
        return $$"""
            {
                {{GroupBySupport.IndentBlock(range.RenderArgumentPreparation(start,count),"    ").TrimStart()}}
                var {{end}}=(long){{start}}+{{count}};
                {{GroupBySupport.IndentBlock(this.Intervals(start,end,negativeEnd,positiveStart,positiveEnd,skipZero),"    ").TrimStart()}}
                for(var {{value}}=(long){{start}};{{value}}<{{negativeEnd}};{{value}}++){
                    {{consumerName}}.Consume((int){{value}});
                }
                for(var {{value}}={{positiveStart}};{{value}}<{{positiveEnd}};{{value}}++){
                    if({{skipZero}}&&{{value}}%{{modulus}}==0){
                        continue;
                    }
                    {{consumerName}}.Consume((int){{value}});
                }
            }
            """;
    }

    /*
    Range の MaxBy/MinBy(x => x%C)。最初に現れる最大(最小)キーの要素を返す(System.Linq と同じく同じキーなら先の要素)。
    start≧0 ならキーは start%C から1ずつ増えて C で 0 に戻るので、先頭 min(count,C) 個の中で式で決まる。
    負の値を含むときは、各符号の区間の先頭 C 個だけを順に調べる(その先はキーが繰り返すだけで、より先に現れる極値は無い)。
    */
    public static string RenderExtremeBy(RangeRoot range,int modulus,int number,string destVar,string destType,bool max) {
        var start = $"__start{number}";
        var count = $"__count{number}";
        var end = $"__end{number}";
        var first = $"__first{number}";
        var length = $"__length{number}";
        var best = $"__best{number}";
        var bestKey = $"__bestKey{number}";
        var value = $"__value{number}";
        var key = $"__key{number}";
        var closedForm = max
            ? $"{first}+{length}-1<={modulus-1} ? {start}+{length}-1 : {start}+({modulus-1}-{first})"
            : $"{first}==0 ? {start} : ({first}+{length}-1>={modulus} ? {start}+({modulus}-{first}) : {start})";
        var better = max ? ">" : "<";
        return $$"""
            {{destType}} {{destVar}};
            {
                {{GroupBySupport.IndentBlock(range.RenderArgumentPreparation(start,count),"    ").TrimStart()}}
                if({{count}}==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                if({{start}}>=0){
                    var {{first}}={{start}}%{{modulus}};
                    var {{length}}=global::System.Math.Min({{count}},{{modulus}});
                    {{destVar}}={{closedForm}};
                }else{
                    var {{end}}=(long){{start}}+{{count}};
                    var {{best}}={{start}};
                    var {{bestKey}}={{start}}%{{modulus}};
                    var __negativeEnd{{number}}=global::System.Math.Min(global::System.Math.Min({{end}},0L),(long){{start}}+{{modulus}});
                    for(var {{value}}=(long){{start}}+1;{{value}}<__negativeEnd{{number}};{{value}}++){
                        var {{key}}=(int)({{value}}%{{modulus}});
                        if({{key}}{{better}}{{bestKey}}){
                            {{bestKey}}={{key}};
                            {{best}}=(int){{value}};
                        }
                    }
                    var __positiveEnd{{number}}=global::System.Math.Min({{end}},(long){{modulus}});
                    for(var {{value}}=0L;{{value}}<__positiveEnd{{number}};{{value}}++){
                        var {{key}}=(int)({{value}}%{{modulus}});
                        if({{key}}{{better}}{{bestKey}}){
                            {{bestKey}}={{key}};
                            {{best}}=(int){{value}};
                        }
                    }
                    {{destVar}}={{best}};
                }
            }
            """;
    }
}
