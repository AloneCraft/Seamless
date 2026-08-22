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
            return base.GetConsumeExecutionStatement(consumerName);
        }

        var start = $"__start{番号}";
        var count = $"__count{番号}";
        var take = $"__take{番号}";
        var actual = $"__actual{番号}";
        var skip = $"__skip{番号}";
        var index = $"__i{番号}";
        return $$"""
            {
                var {{start}}={{range.StartExpression}};
                var {{count}}={{range.CountExpression}};
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
