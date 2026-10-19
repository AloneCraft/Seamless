namespace Seamless.FLinq.Generator;

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
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.EnumeratorTypeName,source.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => $"Compute(ref {this.e})";
    internal bool CanInlineMaterializeConsume => source is ArrayRoot;
    public override string InlineSupportingMembers {
        get {
            if(source is RangeRoot or ArrayRoot) {
                return string.Empty;
            }
            return source.GetDirectSpanExpression("__source") is not null
                ? source.GetEnumeratorStruct
                : base.InlineSupportingMembers;
        }
    }
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderArrayToListInline(destVar,destType)
        ??this.RenderRangeToListInline(destVar,destType)
        ??this.RenderDirectSpanToListInline(destVar,destType)
        ??this.RenderZipToListInline(destVar,destType)
        ??this.RenderConsumeBasedToListInline(destVar,destType)
        ??this.RenderComputeInline(destVar,destType);
    private string? RenderConsumeBasedToListInline(string destVar,string destType){
        if(source is not (Join or GroupJoin or SelectManyBase)){
            return null;
        }
        var placeholder=$"__FLINQ_TOLIST_{番号}__";
        string consumeStatement;
        try{
            consumeStatement=source.GetConsumeExecutionStatement(placeholder);
        }catch(NotSupportedException){
            return null;
        }
        if(string.IsNullOrWhiteSpace(consumeStatement)
            ||consumeStatement.Contains("TryGetNext")
            ||consumeStatement.Contains("GetRemainingEnumerator")
            ||!consumeStatement.Contains($"{placeholder}.Consume(")){
            return null;
        }
        var escaped=global::System.Text.RegularExpressions.Regex.Escape(placeholder);
        var pattern=$@"(?<indent>[ \t]*){escaped}\.Consume\((?<arg>.+?)\);";
        var replaced=global::System.Text.RegularExpressions.Regex.Replace(consumeStatement,pattern,match => {
            var indent=match.Groups["indent"].Value;
            var arg=match.Groups["arg"].Value;
            return $"{indent}{destVar}.Add({arg});";
        });
        string capacityArg;
        if(source.NonEnumeratedCountExpression is { } countExpr){
            capacityArg=countExpr;
        }else{
            capacityArg=string.Empty;
        }
        return $$"""
            {{destType}} {{destVar}}=new({{capacityArg}});
            {{replaced}}
            """;
    }
    private string? RenderZipToListInline(string destVar,string destType){
        if(source is Zip zip){
            return zip.RenderDirectToListAsync(destVar,destType);
        }
        if(source is Zip_resultSelector zipSel){
            return zipSel.RenderDirectToListAsync(destVar,destType);
        }
        if(source is Zip3 zip3){
            return zip3.RenderDirectToListAsync(destVar,destType);
        }
        return null;
    }
    private string? RenderArrayToListInline(string destVar,string destType) {
        if(source is not ArrayRoot { DirectArrayExpression: { } arrayExpression }) {
            return null;
        }

        var sourceName=$"__array{this.result}";
        var span=$"__span{this.result}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{sourceName}}={{arrayExpression}};
                global::System.ArgumentNullException.ThrowIfNull({{sourceName}},"source");
                {{this.CounterCall("IncrementDirectSpanUsed")}}
                var {{span}}=global::System.MemoryExtensions.AsSpan({{sourceName}});
                {{destVar}}=new({{span}}.Length);
                global::System.Runtime.InteropServices.CollectionsMarshal.SetCount({{destVar}},{{span}}.Length);
                {{this.CounterCall("IncrementFastCopyUsed")}}
                {{span}}.CopyTo(global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan({{destVar}}));
            }
            """;
    }
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
                {{range.RenderArgumentPreparation(start,count)}}
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
        private static {{resultType}} Compute(ref TEnum source){
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
        private static {{resultType}} Compute(ref TEnum source){
            // Compute は列挙子を破棄しない。Count など他の終端と同じく、参照で渡された本体を持ち主
            // (呼び出し元の finally、または終端 enumerator の Dispose)が 1 回だけ破棄する。
            // ここで複製を作って破棄すると、コンストラクタでプール配列を借りる enumerator（OrderBy 等）が
            // 呼び出し元の Dispose と二重 Return になり、後続の無関係なクエリのデータが壊れる。
            ref var enumerator = ref source;
            global::System.Collections.Generic.List<{{source.ElementType}}> __result;
            {
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
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
