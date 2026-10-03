namespace Seamless.FLinq.Generator;

/*
Skip は source.Skip(count) を、初回 TryGetNext で count 件だけ読み捨てる enumerator へ変換する。

入力例:
  source.Skip(5)
  source.Skip(a).Skip(b)

生成例:
  if(!skipped) {
      skipped = true;
      while(count-- > 0 && source.TryGetNext(out var v)) {}
  }
  if(source.TryGetNext(out var v)) {
      current = v;
      return true;
  }

バリエーション:
  - count < 0 は constructor で 0 に丸める。
  - 連続 Skip は RootSource へ畳み込み、count は previous + current を int.MaxValue 上限で合成する。
*/
internal class Skip(int 番号,Enumerable source,string skipCount):Enumerable(番号) {
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
    private string NormalizedSkipCountExpression => $"global::System.Math.Max(0,{this.SkipCountExpression})";
    private Enumerable RootSource => this.Source is Skip skip ? skip.RootSource : this.Source;
    private string EffectiveSkipCount => this.Source is Skip skip
        ? $"(int)global::System.Math.Min(int.MaxValue,(long)({skip.EffectiveSkipCount})+({this.NormalizedSkipCountExpression}))"
        : this.NormalizedSkipCountExpression;
    protected override Enumerable PrimarySource => this.RootSource;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{this.RootSource.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.EnumeratorTypeName,this.RootSource.GetEnumeratorExpression,this.EffectiveSkipCount);
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}, int count";
    public override string GetEnumeratorFields => $$"""
        private bool skipped = false;
        """;
    public override string GetEnumeratorConstructor => "if(this.count < 0) { this.count = 0; }";
    public override string GetTryGetNextBody => $$"""
        if(!this.skipped){
            this.skipped = true;
            var remaining = this.count;
            while(remaining > 0 && {{this.e}}.TryGetNext(out _)){
                remaining--;
            }
        }
        if({{this.e}}.TryGetNext(out var __mn62)){
            current = this.{{this.currentValue}} = __mn62;
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
            var {{this.PreparedRangeSkipCountName}}=global::System.Math.Clamp({{this.EffectiveSkipCount}},0,{{range.PreparedCountName}});
            """;
    }
    private string PreparedRangeSkipCountName => $"__skipRangeSkipCount{番号}";
    public override string GetConsumeExecutionStatement(string consumerName) {
        if(this.RootSource is RangeRoot range) {
            return this.RenderRangeConsume(consumerName,range);
        }
        var sourceName = $"__skipSource{番号}";


        var spanExpression = this.RootSource.GetDirectSpanExpression(sourceName);
        if(spanExpression is null) {
            var skipCountName = $"{sourceName}Count";
            var skipItemName = $"{sourceName}Item";
            return $$"""
                using var {{sourceName}}={{this.RootSource.GetEnumeratorExpression}};
                global::Seamless.Linq.LinqCounterScope.IncrementEnumerationLoopUsed();
                var {{skipCountName}}=global::System.Math.Max(0,{{this.EffectiveSkipCount}});
                while({{skipCountName}}>0&&{{sourceName}}.TryGetNext(out _)){
                    {{skipCountName}}--;
                }
                {{DrainLoopHead(this.RootSource,sourceName,skipItemName)}}
                    {{consumerName}}.Consume({{skipItemName}});
                }
                """;
        }
        return $$"""
            using var {{sourceName}}={{this.RootSource.GetEnumeratorExpression}};
            var __skipSpan{{番号}}={{spanExpression}};
            var __skipCount{{番号}}=global::System.Math.Clamp({{this.EffectiveSkipCount}},0,__skipSpan{{番号}}.Length);
            for(var __skipIndex{{番号}}=__skipCount{{番号}};__skipIndex{{番号}}<__skipSpan{{番号}}.Length;__skipIndex{{番号}}++){
                {{consumerName}}.Consume(__skipSpan{{番号}}[__skipIndex{{番号}}]);
            }
            """;
    }

    private string RenderRangeConsume(string consumerName,RangeRoot range) {
        var rangeOffsetName = $"__skipRangeOffset{番号}";
        return $$"""
            {{this.CounterCall("IncrementEnumerationLoopUsed")}}
            for(var {{rangeOffsetName}}={{this.PreparedRangeSkipCountName}};{{rangeOffsetName}}<{{range.PreparedCountName}};{{rangeOffsetName}}++){
                {{consumerName}}.Consume({{range.PreparedStartName}}+{{rangeOffsetName}});
            }
            """;
    }
}
