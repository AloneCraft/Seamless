namespace Seamless.FLinq;

/*
MinMaxByBase は source.MinBy(keySelector) / MaxBy(keySelector) を、最良 key を持つ元要素へ変換する。

入力例:
  source.MaxBy(x => x.Score)

生成例:
  if(!source.TryGetNext(out var v)) throw NoElements();
  bestElement = v;
  bestKey = keySelector(bestElement);
  while(source.TryGetNext(out var v)) {
      key = keySelector(item);
      if(comparer.Compare(key, bestKey) is better) {
          bestElement = item;
          bestKey = key;
      }
  }
  return bestElement;

バリエーション:
  - span source は最初に span path を使い、だめなら ComputeEnumerator に戻る。
  - direction により > 0 / < 0 を切り替える。
*/
internal abstract class MinMaxByBase(int 番号,Enumerable source,FuncFragment keySelector,string resultType,string direction):BufferedScalar(番号,resultType) {
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
        if(keySelector is { } __visitorChild_keySelector) {
            yield return __visitorChild_keySelector;
        }
    }

    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(keySelector,nameof(keySelector));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => RenderEnumeratorExpressionWithClosure(this.EnumeratorTypeName,new[] { keySelector },source.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(keySelector)}";
    protected override string ComputeExpression => $"Compute({this.e}{this.ClosureArgsSuffix(keySelector)})";
    public override string? RenderInlineComputation(string destVar,string destType) {
        var methodName = direction=="greater" ? "MaxBy" : "MinBy";
        return this.RenderComputeInline(destVar,destType)
            ??(TryRenderDirectLambda(keySelector) is { } lambda
                ? this.RenderDirectEnumerableCallInline(source,destVar,destType,methodName,lambda)
                : null);
    }
    protected override string ComputeMembers => source.CanTryProvideSpan
        ? this.BuildTrySpanCompute()
        : this.BuildEnumeratorCompute();
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
    private string CompareOp => direction=="greater" ? ">" : "<";
    private string BuildSpanCompute() => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(keySelector)}}){
            var comparer=global::System.Collections.Generic.Comparer<{{FuncReturnTypeName(keySelector)}}>.Default;
            source.TryGetSpan(out var __span);
            if(__span.Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
            {{resultType}} bestElement=default!;
            {{FuncReturnTypeName(keySelector)}} bestKey=default!;
            var __first=true;
            foreach(var {{this.item}} in __span){
                {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                {{this.FuncAssignIndented("        ",keySelector,nameof(keySelector),this.key,this.item)}}
                if(__first||comparer.Compare({{this.key}},bestKey){{this.CompareOp}}0){
                    __first=false;
                    bestElement={{this.item}};
                    bestKey={{this.key}};
                }
            }
            return bestElement;
        }
        """;
    private string BuildTrySpanCompute() => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(keySelector)}}){
            if(source.TryGetSpan(out var __span)){
                var comparer=global::System.Collections.Generic.Comparer<{{FuncReturnTypeName(keySelector)}}>.Default;
                if(__span.Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                {{resultType}} bestElement=default!;
                {{FuncReturnTypeName(keySelector)}} bestKey=default!;
                var __first=true;
                foreach(var {{this.item}} in __span){
                    {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                    {{this.FuncAssignIndented("            ",keySelector,nameof(keySelector),this.key,this.item)}}
                    if(__first||comparer.Compare({{this.key}},bestKey){{this.CompareOp}}0){
                        __first=false;
                        bestElement={{this.item}};
                        bestKey={{this.key}};
                    }
                }
                return bestElement;
            }
            return ComputeEnumerator(source{{this.ClosureArgsSuffix(keySelector)}});
        }
        {{AggressiveInliningAttribute}}
        private static {{resultType}} ComputeEnumerator(TEnum source{{this.ClosureMethodParameterSuffix(keySelector)}}){
            var comparer=global::System.Collections.Generic.Comparer<{{FuncReturnTypeName(keySelector)}}>.Default;
            if(!source.TryGetNext(out var __mn104)) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
            {{resultType}} bestElement=__mn104;
            {{FuncReturnTypeName(keySelector)}} bestKey=default!;
            {{this.FuncAssignIndented("    ",keySelector,nameof(keySelector),"bestKey","bestElement")}}
            while(source.TryGetNext(out var __mn105)){
                var {{this.item}}=__mn105;
                {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                {{this.FuncAssignIndented("        ",keySelector,nameof(keySelector),this.key,this.item)}}
                if(comparer.Compare({{this.key}},bestKey){{this.CompareOp}}0){
                    bestElement={{this.item}};
                    bestKey={{this.key}};
                }
            }
            return bestElement;
        }
        """;
    private string BuildEnumeratorCompute() => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(keySelector)}}){
            var comparer=global::System.Collections.Generic.Comparer<{{FuncReturnTypeName(keySelector)}}>.Default;
            if(!source.TryGetNext(out var __mn106)) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
            {{resultType}} bestElement=__mn106;
            {{FuncReturnTypeName(keySelector)}} bestKey=default!;
            {{this.FuncAssignIndented("    ",keySelector,nameof(keySelector),"bestKey","bestElement")}}
            while(source.TryGetNext(out var __mn107)){
                var {{this.item}}=__mn107;
                {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                {{this.FuncAssignIndented("        ",keySelector,nameof(keySelector),this.key,this.item)}}
                if(comparer.Compare({{this.key}},bestKey){{this.CompareOp}}0){
                    bestElement={{this.item}};
                    bestKey={{this.key}};
                }
            }
            return bestElement;
        }
        """;
}
