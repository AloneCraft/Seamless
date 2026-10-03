namespace Seamless.FLinq.Generator;
/*
MinMaxSelectorBase は source.Min(selector) / Max(selector) を selector 結果の最小/最大にする。

入力例:
  source.Max(x => x.Score)

生成例:
  if(!source.TryGetNext(out var v)) throw NoElements();
  result = selector(v);
  while(source.TryGetNext(out var v)) {
      value = selector(v);
      if(comparer.Compare(value, result) is better) result = value;
  }
  return result;

バリエーション:
  - source が TryGetSpan 可能なら span loop を試す。
  - direction により comparer.Compare(value, result) > 0 / < 0 を切り替える。
*/
internal abstract class MinMaxSelectorBase(int 番号,Enumerable source,FuncFragment selector,string resultType,string direction):BufferedScalar(番号,resultType) {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
    }
    protected override IEnumerable<FuncFragment> GetChildFunctions() {
        foreach(var function in base.GetChildFunctions()) {
                yield return function;
        }
        if(selector is { } __visitorChild_selector) {
            yield return __visitorChild_selector;
        }
    }

    private string SelectorFuncType => $"global::System.Func<{source.ElementType}, {FuncReturnTypeName(selector)}>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(selector,nameof(selector));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        RenderEnumeratorExpressionWithClosure(this.EnumeratorTypeName,new[] { selector },source.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(selector)}";
    protected override string ComputeExpression =>
        $"Compute(ref {this.e}{this.ClosureArgsSuffix(selector)})";
    public override string? RenderInlineComputation(string destVar,string destType) {
        // Range の1次式の選択子は単調なので、最大・最小は端の値(両端が int に収まるとき)。
        if(source is RangeRoot range
            &&resultType is "int" or "long" or "double"
            &&RangeArithmeticOptimizer.TryParseLinear(selector,out var linear)
            &&(linear.Conversion??"int")==resultType) {
            return RangeArithmeticOptimizer.RenderLinearMinMax(range,linear,番号,destVar,destType,resultType,direction=="greater",
                (destination,argument) => this.FuncAssign(selector,nameof(selector),destination,argument));
        }
        var methodName = direction=="greater" ? "Max" : "Min";
        return this.RenderComputeInline(destVar,destType)
            ??(TryRenderDirectLambda(selector) is { } lambda
                ? this.RenderDirectEnumerableCallInline(source,destVar,destType,methodName,lambda)
                : null);
    }
    private string CompareCond => direction=="greater" ? "comparer.Compare(value,result)>0" : "comparer.Compare(value,result)<0";
    protected override string ComputeMembers => source.CanTryProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(ref TEnum source{{this.ClosureMethodParameterSuffix(selector)}}){
                var comparer=global::System.Collections.Generic.Comparer<{{resultType}}>.Default;
                {{resultType}} result=default!;
                if(source.TryGetSpan(out var __span)){
                    if(__span.Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                    var __first=true;
                    foreach(var {{this.item}} in __span){
                        {{resultType}} value=default!;
                        {{this.FuncAssignIndented("            ",selector,nameof(selector),"value",this.item)}}
                        if(__first||{{this.CompareCond}}){
                            __first=false;
                            result=value;
                        }
                    }
                }else{
                    {{source.ElementType}} {{this.item}};
                    if(!source.TryGetNext(out var __mn108)) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                    {{this.item}}=__mn108;
                    {{this.FuncAssignIndented("        ",selector,nameof(selector),"result",this.item)}}
                    while(source.TryGetNext(out var __mn109)){
                        {{this.item}}=__mn109;
                        {{resultType}} value=default!;
                        {{this.FuncAssignIndented("            ",selector,nameof(selector),"value",this.item)}}
                        if({{this.CompareCond}}) result=value;
                    }
                }
                return result;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(ref TEnum source{{this.ClosureMethodParameterSuffix(selector)}}){
                var comparer=global::System.Collections.Generic.Comparer<{{resultType}}>.Default;
                {{source.ElementType}} {{this.item}};
                {{resultType}} result=default!;
                if(!source.TryGetNext(out var __mn110)) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                {{this.item}}=__mn110;
                {{this.FuncAssignIndented("    ",selector,nameof(selector),"result",this.item)}}
                while(source.TryGetNext(out var __mn111)){
                    {{this.item}}=__mn111;
                    {{resultType}} value=default!;
                    {{this.FuncAssignIndented("        ",selector,nameof(selector),"value",this.item)}}
                    if({{this.CompareCond}}) result=value;
                }
                return result;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
