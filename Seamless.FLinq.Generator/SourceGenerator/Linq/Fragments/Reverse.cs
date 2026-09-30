namespace Seamless.FLinq.Generator;

/*
Reverse は source.Reverse() を、初回に逆順で読める状態を作ってから末尾から返す enumerator へ変換する。

入力例:
  source.Reverse()
  source.Reverse().Reverse()

生成例:
  if(notInitialized) {
      if(source.TryGetSpan(out var span)) index = span.Length - 1;
      else buffer = source.ToList(); index = buffer.Count - 1;
  }
  if(index >= 0) return spanOrBuffer[index--];
  return false;

バリエーション:
  - source が span を返せる場合は buffer を作らず span の末尾から読む。
  - span が無い場合は List に全件 buffer する。
  - 連続 Reverse は偶数回なら RootSource そのものへ畳み込む。
*/
internal class Reverse(int 番号,Enumerable source):Enumerable(番号) {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
    }

    private Enumerable Source => source;
    private Enumerable RootSource => this.Source is Reverse reverse ? reverse.RootSource : this.Source;
    private bool IsIdentity => this.Source is Reverse reverse&&!reverse.IsIdentity;
    private string groupCount => $"{this.group}Count";
    private string groupReturn => $"{this.group}Return";
    protected override Enumerable PrimarySource => this.RootSource;
    public override string StructMethod => string.Empty;
    public override string GetEnumerableExpression =>
        this.IsIdentity
            ? this.RootSource.GetEnumerableExpression
            : $"global::Seamless.Linq.Enumerable.Reverse({this.RootSource.GetEnumerableExpression})";
    public override string GetEnumeratorStruct => $$"""
        {{this.RootSource.GetEnumeratorStruct}}
        {{(this.IsIdentity ? string.Empty : base.GetEnumeratorStruct)}}
        """;
    public override string EnumeratorTypeName => this.IsIdentity
        ? this.RootSource.EnumeratorTypeName
        : base.EnumeratorTypeName;
    public override string GetEnumeratorExpression => this.IsIdentity
        ? this.RootSource.GetEnumeratorExpression
        : RenderEnumeratorExpression(this.EnumeratorTypeName,this.RootSource.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    public override string GetEnumeratorFields => $$"""
        private int {{this.a}}=int.MinValue;
        private {{this.RootSource.ElementType}}[]? {{this.group}};
        private int {{this.groupCount}};
        """;
    // span を取れない root（IEnumerable<T> 等）は全件バッファが要る。List<T> は要素数ぶんの
    // GC ヒープ確保になるため、OrderBy と同じ ArrayPool 配管（PooledBufferSupport）を使う。
    public override string GetTryGetNextBody => this.RootSource.CanTryProvideSpan
        ? $$"""
            // {{this.a}} は int.MinValue=未初期化 / -1=列挙完了 を区別する。
            // -1 を未初期化扱いにすると、TryGetNext を完了後も呼ぶ消費側（TraversalSupport の while ループ等）で
            // 末尾到達ごとに Length-1 へ再初期化して無限ループになる。
            if(this.{{this.a}}==int.MinValue){
                if({{this.e}}.TryGetSpan(out var __span0)){
                    this.{{this.a}}=__span0.Length-1;
                }else{
            {{PipelineEnumerable.IndentBlock(PooledBufferSupport.BuildStart(this.e,this.RootSource.ElementType,this.group,this.groupCount,this.groupReturn,$"{番号}"),"        ")}}
                    while({{this.e}}.TryGetNext(out var __mn46)){
            {{PipelineEnumerable.IndentBlock(PooledBufferSupport.Add(this.RootSource.ElementType,this.group,this.groupCount,"__mn46",$"{番号}"),"            ")}}
                    }
            {{PipelineEnumerable.IndentBlock(PooledBufferSupport.Commit(this.group,this.groupCount),"        ")}}
                    this.{{this.a}}=this.{{this.groupCount}}-1;
            {{PipelineEnumerable.IndentBlock(PooledBufferSupport.Finally(this.RootSource.ElementType,this.group,this.groupReturn),"        ")}}
                }
            }
            if(this.{{this.a}}>=0){
                // span 経路かバッファ経路かは最初の判定で決まっている(バッファ経路なら group が入っている)。ここで TryGetSpan を
                // 引き直してはいけない: DefaultIfEmpty のように、要素を返したあとで答えが変わる source があり、空の span を読んで例外になる。
                if(this.{{this.group}} is null){
                    {{this.e}}.TryGetSpan(out var __span);
                    current=this.{{this.currentValue}}=__span[this.{{this.a}}--];
                }else{
                    current=this.{{this.currentValue}}=this.{{this.group}}![this.{{this.a}}--];
                }
                return true;
            }
            current=default!;
            return false;
            """
        : $$"""
            if(this.{{this.group}} is null){
            {{PipelineEnumerable.IndentBlock(PooledBufferSupport.BuildStart(this.e,this.RootSource.ElementType,this.group,this.groupCount,this.groupReturn,$"{番号}"),"    ")}}
                while({{this.e}}.TryGetNext(out var __mn47)){
            {{PipelineEnumerable.IndentBlock(PooledBufferSupport.Add(this.RootSource.ElementType,this.group,this.groupCount,"__mn47",$"{番号}"),"        ")}}
                }
            {{PipelineEnumerable.IndentBlock(PooledBufferSupport.Commit(this.group,this.groupCount),"    ")}}
                this.{{this.a}}=this.{{this.groupCount}}-1;
            {{PipelineEnumerable.IndentBlock(PooledBufferSupport.Finally(this.RootSource.ElementType,this.group,this.groupReturn),"    ")}}
            }
            if(this.{{this.a}}>=0){
                current=this.{{this.currentValue}}=this.{{this.group}}[this.{{this.a}}--];
                return true;
            }
            current=default!;
            return false;
            """;
    protected override string GetEnumeratorDisposeBody => this.IsIdentity
        ? base.GetEnumeratorDisposeBody
        : $$"""
            {{this.e}}.Dispose();
            {{PooledBufferSupport.Dispose(this.RootSource.ElementType,this.group)}}
            """;
    public override string ElementType => this.RootSource.ElementType;
    public override string GetConsumePreparationStatement() =>
        this.IsIdentity
            ? this.RootSource.GetConsumePreparationStatement()
            : base.GetConsumePreparationStatement();
    public override string GetConsumeExecutionStatement(string consumerName) {
        if(this.IsIdentity) {
            return this.RootSource.GetConsumeExecutionStatement(consumerName);
        }
        var sourceName = $"__reverseSource{番号}";
        var directSpanExpression = this.RootSource.GetDirectSpanExpression(sourceName);
        if(directSpanExpression is not null) {
            var spanName = $"__reverseSpan{番号}";
            return $$"""
                using var {{sourceName}}={{this.RootSource.GetEnumeratorExpression}};
                global::Seamless.Linq.LinqCounterScope.IncrementDirectSpanUsed();
                var {{spanName}}={{directSpanExpression}};
                for(var __i{{番号}}={{spanName}}.Length-1;__i{{番号}}>=0;__i{{番号}}--){
                    {{consumerName}}.Consume({{spanName}}[__i{{番号}}]);
                }
                """;
        }
        if(this.RootSource is RangeRoot range) {
            // Range は start+i で各要素を計算できるので、逆順消費にバッファは一切要らない。
            // span を返せない root だからと List へ全件溜めると、要素数ぶんのヒープ確保が丸ごと無駄になる。
            return $$"""
                {
                    var __reverseStart{{番号}}={{range.StartExpression}};
                    var __reverseCount{{番号}}={{range.CountExpression}};
                    for(var __i{{番号}}=__reverseCount{{番号}}-1;__i{{番号}}>=0;__i{{番号}}--){
                        {{consumerName}}.Consume(__reverseStart{{番号}} + __i{{番号}});
                    }
                }
                """;
        }
        return base.GetConsumeExecutionStatement(consumerName);
    }
}
