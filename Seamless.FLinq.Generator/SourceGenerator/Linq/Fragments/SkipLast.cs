namespace Seamless.FLinq.Generator;

internal class SkipLast(int 番号,Enumerable source,string skipCount):Enumerable(番号) {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
    }

    public static bool IsApplicableVariant(IMethodSymbol method) =>
        method.Parameters.Length==1&&method.Parameters[0].Type.SpecialType==SpecialType.System_Int32;
    private Enumerable Source => source;
    private string SkipCountExpression => skipCount;
    private Enumerable RootSource => this.Source is SkipLast skipLast ? skipLast.RootSource : this.Source;
    private string EffectiveSkipCount => this.Source is SkipLast skipLast
        ? $"(int)global::System.Math.Min(int.MaxValue,(long)global::System.Math.Max(0,{skipLast.EffectiveSkipCount})+global::System.Math.Max(0,{this.SkipCountExpression}))"
        : this.SkipCountExpression;
    protected override Enumerable PrimarySource => this.RootSource;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{this.RootSource.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.EnumeratorTypeName,this.RootSource.GetEnumeratorExpression,this.EffectiveSkipCount);
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}, int count";
    // 直近 N 件のスライディングウィンドウは ArrayPool ベースの ValueQueue で持つ。
    // GC ヒープの Queue<T> だと要素数+1 の配列を毎回確保していた（ClassInt32・N=1024 で 8,216B）。
    // ValueQueue は struct なのでオブジェクト確保もゼロだが、フィールド初期化子は置かずに
    // 初回 TryGetNext まで借りない（未列挙のまま捨てられたときに取りこぼさないため）。
    // 構築済みかどうかは既存の {yielded} が正確に表しているので、判定はそれに寄せる。
    private string LookupField => $"private global::Seamless.Linq.ValueQueue<{this.RootSource.ElementType}> {this.lookup};";
    public override string GetEnumeratorFields => this.RootSource.CanTryProvideSpan
        ? $$"""
            private int {{this.a}};
            private int {{this.temp}}=-1;
            {{this.LookupField}}
            private bool {{this.yielded}};
            """
        : $$"""
            {{this.LookupField}}
            private bool {{this.yielded}};
            """;
    // default(ValueQueue<T>) は items が null で Dispose が no-op になるため、
    // 構築済み判定なしで無条件に呼んでよい。
    protected override string GetEnumeratorDisposeBody => $$"""
        this.{{this.lookup}}.Dispose();
        {{this.e}}.Dispose();
        """;
    public override string GetTryGetNextBody => this.RootSource.CanTryProvideSpan
        ? $$"""
            if({{this.e}}.TryGetSpan(out var __span)){
                if(this.{{this.temp}}<0){
                    this.{{this.temp}}=__span.Length-global::System.Math.Min(count<0?0:count,__span.Length);
                }
                if((uint)this.{{this.a}}<(uint)this.{{this.temp}}){
                    current=this.{{this.currentValue}}=__span[this.{{this.a}}++];
                    return true;
                }
                current=default!;
                return false;
            }
            var __c=count<0?0:count;
            if(__c==0){
                if({{this.e}}.TryGetNext(out var __mn63)){
                    current=this.{{this.currentValue}}=__mn63;
                    return true;
                }
                current=default!;
                return false;
            }
            if(!this.{{this.yielded}}){
                this.{{this.lookup}}=new global::Seamless.Linq.ValueQueue<{{this.RootSource.ElementType}}>(__c+1);
                while(this.{{this.lookup}}.Count<__c&&{{this.e}}.TryGetNext(out var __mn64)) this.{{this.lookup}}.Enqueue(__mn64);
                this.{{this.yielded}}=true;
            }
            if(this.{{this.yielded}}&&{{this.e}}.TryGetNext(out var __mn65)){
                this.{{this.lookup}}.Enqueue(__mn65);
                current=this.{{this.currentValue}}=this.{{this.lookup}}.Dequeue();
                return true;
            }
            current=default!;
            return false;
            """
        : $$"""
            var __c=count<0?0:count;
            if(__c==0){
                if({{this.e}}.TryGetNext(out var __mn66)){
                    current=this.{{this.currentValue}}=__mn66;
                    return true;
                }
                current=default!;
                return false;
            }
            if(!this.{{this.yielded}}){
                this.{{this.lookup}}=new global::Seamless.Linq.ValueQueue<{{this.RootSource.ElementType}}>(__c+1);
                while(this.{{this.lookup}}.Count<__c&&{{this.e}}.TryGetNext(out var __mn67)) this.{{this.lookup}}.Enqueue(__mn67);
                this.{{this.yielded}}=true;
            }
            if(this.{{this.yielded}}&&{{this.e}}.TryGetNext(out var __mn68)){
                this.{{this.lookup}}.Enqueue(__mn68);
                current=this.{{this.currentValue}}=this.{{this.lookup}}.Dequeue();
                return true;
            }
            current=default!;
            return false;
            """;
    public override string ElementType => this.RootSource.ElementType;
    public override string GetConsumeExecutionStatement(string consumerName) {
        // Range の SkipLast は先頭からの短い Range(待ち行列は要らない)。評価順は Range の引数、捨てる件数の順。
        if(this.RootSource is RangeRoot range) {
            var start = $"__skipLastStart{番号}";
            var count = $"__skipLastRangeCount{番号}";
            var keep = $"__skipLastKeep{番号}";
            return $$"""
                {
                    {{GroupBySupport.IndentBlock(range.RenderArgumentPreparation(start,count),"    ").TrimStart()}}
                    var {{keep}}={{count}}-global::System.Math.Clamp({{this.EffectiveSkipCount}},0,{{count}});
                    for(var __skipLastIndex{{番号}}=0;__skipLastIndex{{番号}}<{{keep}};__skipLastIndex{{番号}}++){
                        {{consumerName}}.Consume({{start}}+__skipLastIndex{{番号}});
                    }
                }
                """;
        }
        var sourceName = $"__skipLastSource{番号}";
        var spanExpression = this.RootSource.GetDirectSpanExpression(sourceName);
        if(spanExpression is null) {
            // span の無い根は、根の列挙子を差し替えメソッドから回し、捨てる件数を超えた分を待ち行列から流す。
            // SkipLast の列挙子の構造体から根の構造体を呼ぶ入れ子は、参照型の共有ジェネリックコードで内側がインライン化されない。
            string rootExpression;
            try {
                rootExpression=this.RootSource.GetEnumeratorExpression;
            } catch(NotSupportedException) {
                return base.GetConsumeExecutionStatement(consumerName);
            }
            var queue = $"__skipLastQueue{番号}";
            var skip = $"__skipLastCount{番号}";
            var item = $"__skipLastItem{番号}";
            return $$"""
                using var {{sourceName}}={{rootExpression}};
                var {{skip}}=global::System.Math.Max(0,{{this.EffectiveSkipCount}});
                using var {{queue}}=new global::Seamless.Linq.ValueQueue<{{this.ElementType}}>((int)global::System.Math.Min((long){{skip}}+1,1024));
                while({{sourceName}}.TryGetNext(out var {{item}})){
                    {{queue}}.Enqueue({{item}});
                    if({{queue}}.Count>{{skip}}){
                        {{consumerName}}.Consume({{queue}}.Dequeue());
                    }
                }
                """;
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
