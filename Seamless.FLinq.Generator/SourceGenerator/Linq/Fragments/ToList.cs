namespace Seamless.FLinq;

// 要素が匿名型（ElementType=="TResult"）のチェーンを materialize する ToList。
// 匿名型の型名は生成コードに書けないため、Carrier の汎用 ToList へ型推論で委譲し
// var 宣言の直接実行文として出力する（List の要素型は呼び出し側の推論で閉じる）。
/*
AnonymousExitToList は匿名型要素の ToList を direct execution だけで出す特殊終端。

入力例:
  source.Select(x => new { x.Id }).ToList()

生成例:
  var result = Enumerable.ToList(sourceValueEnumerable);

設計意図:
  匿名型の名前は生成メソッドの戻り値型や補助 struct に書けないため、var と型推論に寄せる。
*/
internal sealed class AnonymousExitToList(int 番号,Enumerable source):Scalar(番号), DirectExecutionFragment {
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
    public override string GetEnumeratorStruct => source.GetEnumeratorStruct;
    public override string GetTryGetNextBody => string.Empty;
    public string GetDirectExecutionStatement(string actualVariableName,string actualTypeName) =>
        $"var {actualVariableName}=global::Seamless.Linq.Enumerable.ToList({source.GetEnumerableExpression});";
}

/*
ToList は source.ToList() を、capacity hint と Span copy を使う Compute に変換する。

入力例:
  source.ToList()

生成例:
  if(source.TryGetNonEnumeratedCount(out var count)) {
      list = new List<T>(count);
      CollectionsMarshal.SetCount(list, count);
      if(!source.TryCopyTo(CollectionsMarshal.AsSpan(list), 0)) fill manually;
  } else if(source.TryGetSpan(out var span)) {
      list = new List<T>(span.Length);
      copy span into internal array;
  } else {
      list = GeneratedCodeHelpers.ToListFromValueEnumerator(ref source);
  }

バリエーション:
  - RangeRoot<int> は SetCount 後の Span に start+i を直接代入する。
  - DirectEnumerableExpression が array/list/ICollection なら hybrid inline で余分な enumerator を避ける。
*/
internal class ToList(int 番号,Enumerable source,string resultType):BufferedScalar(番号,resultType) {
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
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => $"Compute({this.e})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderRangeToListInline(destVar,destType)
        ??this.RenderDirectSpanToListInline(destVar,destType)
        ??this.RenderComputeInline(destVar,destType);
    private string? RenderDirectSpanToListInline(string destVar,string destType) {
        var span = $"__span{this.result}";
        return this.RenderSpanInline(source,destVar,destType,span,spanName => $$"""
            {{destVar}}=new({{spanName}}.Length);
            global::System.Runtime.InteropServices.CollectionsMarshal.SetCount({{destVar}},{{spanName}}.Length);
            global::Seamless.Linq.LinqCounterScope.IncrementFastCopyUsed();
            {{spanName}}.CopyTo(global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan({{destVar}}));
            """);
    }
    private string? RenderRangeToListInline(string destVar,string destType) {
        if(source is not RangeRoot range||source.ElementType!="int") {
            return null;
        }

        var start = $"__start{番号}";
        var count = $"__count{番号}";
        var span = $"__span{番号}";
        var index = $"__i{番号}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{start}}={{range.StartExpression}};
                var {{count}}={{range.CountExpression}};
                {{destVar}}=new({{count}});
                global::System.Runtime.InteropServices.CollectionsMarshal.SetCount({{destVar}},{{count}});
                var {{span}}=global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan({{destVar}});
                global::Seamless.Linq.LinqCounterScope.IncrementFastCopyUsed();
                for(var {{index}}=0;{{index}}<{{span}}.Length;{{index}}++){
                    {{span}}[{{index}}]={{start}} + {{index}};
                }
            }
            """;
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source){
            source.TryGetSpan(out var __sourceSpan);
            var __result=new global::System.Collections.Generic.List<{{source.ElementType}}>(__sourceSpan.Length);
            global::System.Runtime.InteropServices.CollectionsMarshal.SetCount(__result,__sourceSpan.Length);
            global::Seamless.Linq.LinqCounterScope.IncrementTryGetSpanCalled();
            global::Seamless.Linq.LinqCounterScope.IncrementTryGetSpanSucceeded();
            global::Seamless.Linq.LinqCounterScope.IncrementFastCopyUsed();
            __sourceSpan.CopyTo(global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan(__result));
            return __result;
        }
        """
        : $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source){
            // using ではなく try-finally なのは、遅経路のヘルパーへ enumerator を ref で渡すため。
            // using ローカルは読み取り専用で ref に渡せず、値渡しにするとコンストラクタで
            // プール配列を借りる enumerator（OrderBy 等）で二重 Return になる。
            var enumerator = source;
            global::System.Collections.Generic.List<{{source.ElementType}}> __result;
            try {
                if(enumerator.TryGetNonEnumeratedCount(out var __count)){
                    __result=new global::System.Collections.Generic.List<{{source.ElementType}}>(__count);
                    global::System.Runtime.InteropServices.CollectionsMarshal.SetCount(__result,__count);
                    var __destination=global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan(__result);
                    if(!enumerator.TryCopyTo(__destination,0)){
                        var __i=0;
                        while(enumerator.TryGetNext(out var __mn88)){
                            __destination[__i++]=__mn88;
                        }
                    }
                    else {
                        global::Seamless.Linq.LinqCounterScope.IncrementFastCopyUsed();
                    }
                    return __result;
                }
                global::Seamless.Linq.LinqCounterScope.IncrementTryGetSpanCalled();
                if(enumerator.TryGetSpan(out var __sourceSpan)){
                    global::Seamless.Linq.LinqCounterScope.IncrementTryGetSpanSucceeded();
                    __result=new global::System.Collections.Generic.List<{{source.ElementType}}>(__sourceSpan.Length);
                    global::System.Runtime.InteropServices.CollectionsMarshal.SetCount(__result,__sourceSpan.Length);
                    global::Seamless.Linq.LinqCounterScope.IncrementFastCopyUsed();
                    __sourceSpan.CopyTo(global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan(__result));
                    return __result;
                }
                global::Seamless.Linq.LinqCounterScope.IncrementTryGetSpanFailed();
                global::Seamless.Linq.LinqCounterScope.IncrementEnumerationLoopUsed();
                return global::Seamless.Linq.GeneratedCodeHelpers.ToListFromValueEnumerator<TEnum,{{source.ElementType}}>(ref enumerator);
            }
            finally {
                enumerator.Dispose();
            }
        }
        """;
    protected override string GetEnumeratorDisposeBody => string.Empty;
}
