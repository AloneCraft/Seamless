namespace Seamless.FLinq.Generator;

/*
Take は source.Take(count) を、返した件数を数える enumerator へ変換する。

入力例:
  source.Take(10)
  source.Take(a).Take(b)

生成例:
  if(returned >= count) return false;
  if(source.TryGetNext(out var v)) {
      returned++;
      current = v;
      return true;
  }
  return false;

バリエーション:
  - count < 0 は constructor で 0 に丸める。
  - 連続 Take は RootSource へ畳み込み、count は Math.Min(previous, current) にする。
*/
internal class Take:Enumerable {
    private readonly int 番号;
    private readonly Enumerable source;
    private readonly string takeCount;

    public Take(int 番号,Enumerable source,string takeCount):base(番号) {
        this.番号=番号;
        this.source=source;
        this.takeCount=takeCount;
        if(this.RootSource is OrderingBase ordering) {
            ordering.MaxTakeCountExpression=this.EffectiveTakeCount;
        }
    }

    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(this.source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
    }

    public static bool IsApplicableVariant(IMethodSymbol method) =>
        method.Parameters.Length==1&&method.Parameters[0].Type.SpecialType==SpecialType.System_Int32;
    private Enumerable Source => this.source;
    private string TakeCountExpression => this.takeCount;
    private Enumerable RootSource => this.Source is Take take ? take.RootSource : this.Source;
    private string EffectiveTakeCount => this.Source is Take take
        ? $"global::System.Math.Min({take.EffectiveTakeCount},{this.TakeCountExpression})"
        : this.TakeCountExpression;
    protected override Enumerable PrimarySource => this.RootSource;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{this.RootSource.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.EnumeratorTypeName,this.RootSource.GetEnumeratorExpression,this.EffectiveTakeCount);
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}, int count";
    public override string GetEnumeratorFields => $$"""
        private int {{this.a}} = 0;
        """;
    public override string GetEnumeratorConstructor => "if(this.count < 0) { this.count = 0; }";
    public override string GetTryGetNextBody => $$"""
        if(this.{{this.a}} >= this.count){
            current=default!;
            return false;
        }
        if({{this.e}}.TryGetNext(out var __mn75)){
            this.{{this.a}}++;
            current = this.{{this.currentValue}} = __mn75;
            return true;
        }
        current=default!;
        return false;
        """;
    public override string ElementType => this.Source.ElementType;
    public override string GetConsumePreparationStatement() {
        if(this.RootSource is not RangeRoot range) {
            return base.GetConsumePreparationStatement();
        }
        return $$"""
            {{range.RenderArgumentPreparation(range.PreparedStartName,range.PreparedCountName)}}
            var {{this.PreparedRangeTakeCountName}}=global::System.Math.Clamp({{this.EffectiveTakeCount}},0,{{range.PreparedCountName}});
            """;
    }
    private string PreparedRangeTakeCountName => $"__takeRangeTakeCount{番号}";
    public override string GetConsumeExecutionStatement(string consumerName) {
        if(this.RootSource is RangeRoot range) {
            return this.RenderRangeConsume(consumerName,range);
        }
        var sourceName = $"__takeSource{番号}";
        var spanExpression = this.RootSource.GetDirectSpanExpression(sourceName);
        if(spanExpression is null) {
            var takeCountName = $"{sourceName}Count";
            var takeItemName = $"{sourceName}Item";
            return $$"""
                using var {{sourceName}}={{this.RootSource.GetEnumeratorExpression}};
                global::Seamless.Linq.LinqCounterScope.IncrementEnumerationLoopUsed();
                var {{takeCountName}}=global::System.Math.Max(0,{{this.EffectiveTakeCount}});
                while({{takeCountName}}>0&&{{sourceName}}.TryGetNext(out var {{takeItemName}})){
                    {{consumerName}}.Consume({{takeItemName}});
                    {{takeCountName}}--;
                }
                """;
        }
        return $$"""
            using var {{sourceName}}={{this.RootSource.GetEnumeratorExpression}};
            var __takeSpan{{番号}}={{spanExpression}};
            var __takeCount{{番号}}=global::System.Math.Clamp({{this.EffectiveTakeCount}},0,__takeSpan{{番号}}.Length);
            for(var __takeIndex{{番号}}=0;__takeIndex{{番号}}<__takeCount{{番号}};__takeIndex{{番号}}++){
                {{consumerName}}.Consume(__takeSpan{{番号}}[__takeIndex{{番号}}]);
            }
            """;
    }

    private string RenderRangeConsume(string consumerName,RangeRoot range) {
        var rangeOffsetName = $"__takeRangeOffset{番号}";
        return $$"""
            {{this.CounterCall("IncrementEnumerationLoopUsed")}}
            for(var {{rangeOffsetName}}=0;{{rangeOffsetName}}<{{this.PreparedRangeTakeCountName}};{{rangeOffsetName}}++){
                {{consumerName}}.Consume({{range.PreparedStartName}}+{{rangeOffsetName}});
            }
            """;
    }
}
