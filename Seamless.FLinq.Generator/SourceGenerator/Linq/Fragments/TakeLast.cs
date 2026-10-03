namespace Seamless.FLinq.Generator;

internal class TakeLast(int 番号,Enumerable source,string takeCount):Enumerable(番号) {
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
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.EnumeratorTypeName,this.RootSource.GetEnumeratorExpression,this.EffectiveTakeCount);
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}, int count";
    // 末尾 N 件のバッファは ArrayPool ベースの ValueQueue で持つ。
    // GC ヒープの Queue<T> だと要素数分の配列を毎回確保していた（ClassInt32・N=1024 で 8,216B）。
    // ValueQueue は struct なのでオブジェクト確保もゼロだが、フィールド初期化子は置かずに
    // 初回 TryGetNext まで借りない（未列挙のまま捨てられたときに取りこぼさないため）。
    // 構築済みかどうかは {yielded} で持つ（null 判定が使えなくなるため）。
    private string LookupField => $"private global::Seamless.Linq.ValueQueue<{this.RootSource.ElementType}> {this.lookup};";
    public override string GetEnumeratorFields => this.RootSource.CanTryProvideSpan
        ? $$"""
            private int {{this.a}}=-1;
            private int {{this.temp}};
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
                if(this.{{this.a}}<0){
                    var __spanCount=count<0?0:count;
                    this.{{this.a}}=__span.Length-global::System.Math.Min(__spanCount,__span.Length);
                    this.{{this.temp}}=__span.Length;
                }
                if((uint)this.{{this.a}}<(uint)this.{{this.temp}}){
                    current=this.{{this.currentValue}}=__span[this.{{this.a}}++];
                    return true;
                }
                current=default!;
                return false;
            }
            var __takeCount=count<0?0:count;
            if(!this.{{this.yielded}}){
                this.{{this.yielded}}=true;
                // Rent(0) は Array.Empty<T>() を返すので 0 件では借りない。
                if(__takeCount>0){
                    this.{{this.lookup}}=new global::Seamless.Linq.ValueQueue<{{this.RootSource.ElementType}}>(__takeCount);
                    while({{this.e}}.TryGetNext(out var __mn76)){
                        if(this.{{this.lookup}}.Count==__takeCount) this.{{this.lookup}}.Dequeue();
                        this.{{this.lookup}}.Enqueue(__mn76);
                    }
                }
            }
            if(this.{{this.lookup}}.Count>0){
                current=this.{{this.currentValue}}=this.{{this.lookup}}.Dequeue();
                return true;
            }
            current=default!;
            return false;
            """
        : $$"""
            var __c=count<0?0:count;
            if(!this.{{this.yielded}}){
                this.{{this.yielded}}=true;
                // Rent(0) は Array.Empty<T>() を返すので 0 件では借りない。
                if(__c>0){
                    this.{{this.lookup}}=new global::Seamless.Linq.ValueQueue<{{this.RootSource.ElementType}}>(__c);
                    while({{this.e}}.TryGetNext(out var __mn77)){
                        if(this.{{this.lookup}}.Count==__c) this.{{this.lookup}}.Dequeue();
                        this.{{this.lookup}}.Enqueue(__mn77);
                    }
                }
            }
            if(this.{{this.lookup}}.Count>0){
                current=this.{{this.currentValue}}=this.{{this.lookup}}.Dequeue();
                return true;
            }
            current=default!;
            return false;
            """;
    public override string ElementType => this.RootSource.ElementType;
    public override string GetConsumeExecutionStatement(string consumerName) {
        var sourceName = $"__takeLastSource{番号}";
        var spanExpression = this.RootSource.GetDirectSpanExpression(sourceName);
        if(spanExpression is not null) {
            return $$"""
                using var {{sourceName}}={{this.RootSource.GetEnumeratorExpression}};
                var __takeLastSpan{{番号}}={{spanExpression}};
                var __takeLastCount{{番号}}=global::System.Math.Clamp({{this.EffectiveTakeCount}},0,__takeLastSpan{{番号}}.Length);
                var __takeLastStart{{番号}}=__takeLastSpan{{番号}}.Length-__takeLastCount{{番号}};
                for(var __takeLastIndex{{番号}}=__takeLastStart{{番号}};__takeLastIndex{{番号}}<__takeLastSpan{{番号}}.Length;__takeLastIndex{{番号}}++){
                    {{consumerName}}.Consume(__takeLastSpan{{番号}}[__takeLastIndex{{番号}}]);
                }
                """;
        }
        if(this.RootSource is not RangeRoot range||this.RootSource.ElementType!="int") {
            // span の無い根は、根の列挙子を差し替えメソッドから回し、直近の件数だけを環状の配列に残してから流す。
            // TakeLast の列挙子の構造体から根の構造体を呼ぶ入れ子は、参照型の共有ジェネリックコードで内側がインライン化されない。
            // 待ち行列(取り出してから入れる)ではなく、満ちたら最も古い位置へ上書きする。配列は件数の見込みが立つまで倍々に借り直す。
            string rootExpression;
            try {
                rootExpression=this.RootSource.GetEnumeratorExpression;
            } catch(NotSupportedException) {
                return base.GetConsumeExecutionStatement(consumerName);
            }
            var ring = $"__takeLastRing{番号}";
            var next = $"__takeLastNext{番号}";
            var filled = $"__takeLastFilled{番号}";
            var head = $"__takeLastHead{番号}";
            var offset = $"__takeLastOffset{番号}";
            var position = $"__takeLastPosition{番号}";
            var takeCount = $"__takeLastCount{番号}";
            var item = $"__takeLastItem{番号}";
            var elementType = this.ElementType;
            return $$"""
                using var {{sourceName}}={{rootExpression}};
                var {{takeCount}}=global::System.Math.Max(0,{{this.EffectiveTakeCount}});
                if({{takeCount}}>0){
                    var {{ring}}=global::System.Buffers.ArrayPool<{{elementType}}>.Shared.Rent(global::System.Math.Min({{takeCount}},1024));
                    var {{filled}}=0;
                    var {{head}}=0;
                    try{
                        while({{sourceName}}.TryGetNext(out var {{item}})){
                            if({{filled}}<{{takeCount}}){
                                if({{filled}}=={{ring}}.Length){
                                    var {{next}}=global::System.Buffers.ArrayPool<{{elementType}}>.Shared.Rent((int)global::System.Math.Min((long){{ring}}.Length*2,{{takeCount}}));
                                    global::System.MemoryExtensions.AsSpan({{ring}},0,{{filled}}).CopyTo({{next}});
                                    if(global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{{elementType}}>()) global::System.MemoryExtensions.AsSpan({{ring}},0,{{filled}}).Clear();
                                    global::System.Buffers.ArrayPool<{{elementType}}>.Shared.Return({{ring}});
                                    {{ring}}={{next}};
                                }
                                {{ring}}[{{filled}}++]={{item}};
                                continue;
                            }
                            {{ring}}[{{head}}]={{item}};
                            if(++{{head}}=={{takeCount}}) {{head}}=0;
                        }
                        for(var {{offset}}=0;{{offset}}<{{filled}};{{offset}}++){
                            var {{position}}={{head}}+{{offset}};
                            if({{position}}>={{filled}}) {{position}}-={{filled}};
                            {{consumerName}}.Consume({{ring}}[{{position}}]);
                        }
                    }finally{
                        if(global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{{elementType}}>()) global::System.MemoryExtensions.AsSpan({{ring}},0,{{filled}}).Clear();
                        global::System.Buffers.ArrayPool<{{elementType}}>.Shared.Return({{ring}});
                    }
                }
                """;
        }

        var start = $"__start{番号}";
        var count = $"__count{番号}";
        var take = $"__take{番号}";
        var actual = $"__actual{番号}";
        var skip = $"__skip{番号}";
        var index = $"__i{番号}";
        return $$"""
            {
                {{GroupBySupport.IndentBlock(range.RenderArgumentPreparation(start,count),"    ").TrimStart()}}
                var {{take}}={{this.EffectiveTakeCount}};
                var {{actual}}=global::System.Math.Min({{count}},global::System.Math.Max(0,{{take}}));
                var {{skip}}={{count}} - {{actual}};
                for(var {{index}}=0;{{index}}<{{actual}};{{index}}++){
                    {{consumerName}}.Consume({{start}} + {{skip}} + {{index}});
                }
            }
            """;
    }
}
