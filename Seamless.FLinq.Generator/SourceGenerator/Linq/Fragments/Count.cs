namespace Seamless.FLinq;

/*
Count は Count/LongCount を件数取得 fast path 優先の Compute へ変換する。

入力例:
  source.Count()
  source.LongCount()

生成例:
  if(source.TryGetNonEnumeratedCount(out var count)) return (TResult)count;
  checked {
      while(source.TryGetNext(out var v)) count++;
  }

バリエーション:
  - Range/Repeat は count 式をそのまま resultType へ cast して inline する。
  - source が span を確実に返せる場合は span.Length を返す。
  - LongCount では TryGetNonEnumeratedCount が int なので long へ拡張する。
*/
internal class Count(int 番号,Enumerable source,string resultType):BufferedScalar(番号,resultType) {
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
    protected override string ComputeExpression => $"Compute({this.e})";
    public override string? RenderInlineComputation(string destVar,string destType) {
        if(source is RangeRoot range) {
            return $"{destType} {destVar}=({destType})({range.CountExpression});";
        }
        if(source is RepeatRoot repeat) {
            return $"{destType} {destVar}=({destType})({repeat.CountExpression});";
        }
        if(source.NonEnumeratedCountExpression is { } countExpression) {
            return $"{destType} {destVar}=({destType})({countExpression});";
        }
        return this.RenderDirectEnumerableCountInline(source,destVar,destType)
            ??this.RenderComputeInline(destVar,destType);
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source){
                source.TryGetSpan(out var __span);
                return ({{resultType}})__span.Length;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source){
                if(source.TryGetNonEnumeratedCount(out var __count)){
                    return ({{resultType}})__count;
                }
                {{resultType}} count=0;
                checked{
                    while(source.TryGetNext(out _)) count++;
                }
                return count;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

/*
Count_predicate は predicate が true の件数だけを checked で数える Compute を生成する。

入力例:
  source.Count(x => x.Enabled)

生成例:
  count = 0;
  foreach(var item in source) {
      if(predicate(item)) count++;
  }
  return count;

バリエーション:
  - Range + 単純 predicate は RangePredicateOptimizer が式や短い loop へ inline できる。
  - predicate が直接 lambda として再表示できる場合は DirectEnumerableExpression 経由で
    BufferedScalar の Compute インライン展開に乗る(System.Linq への委譲ではない。
    唯一の RenderDirectEnumerableCallInline override である BufferedScalar.cs 参照)。
*/
internal class Count_predicate(int 番号,Enumerable source,FuncFragment predicate,string resultType):BufferedScalar(番号,resultType) {
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
        if(predicate is { } __visitorChild_predicate) {
            yield return __visitorChild_predicate;
        }
    }

    private string PredicateFuncType => $"global::System.Func<{source.ElementType}, bool>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(predicate,nameof(predicate));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        RenderEnumeratorExpressionWithClosure(this.EnumeratorTypeName,new[] { predicate },source.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(predicate)}";
    protected override string ComputeExpression =>
        $"Compute({this.e}{this.ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorConstructor =>
        $"this.Current = Compute({this.e}{this.ClosureArgsSuffixForCtorBody(predicate)});";
    public override string? RenderInlineComputation(string destVar,string destType) {
        var methodName = resultType is "long" or "global::System.Int64" ? "LongCount" : "Count";
        return source is RangeRoot range
            ? RangePredicateOptimizer.RenderCount(range,predicate,番号,destVar,destType)
                ??(TryRenderDirectLambda(predicate) is { } rangeLambda
                    ? this.RenderDirectEnumerableCallInline(source,destVar,destType,methodName,rangeLambda)??this.RenderComputeInline(destVar,destType)
                    : this.RenderComputeInline(destVar,destType))
        : TryRenderDirectLambda(predicate) is { } lambda
            ? this.RenderDirectEnumerableCallInline(source,destVar,destType,methodName,lambda)??this.RenderComputeInline(destVar,destType)
            : this.RenderComputeInline(destVar,destType);
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}){
                source.TryGetSpan(out var __span);
                var count = 0;
                for(var __i = 0; __i < __span.Length; __i++){
                    var {{this.item}} = __span[__i];
                    bool {{this.temp}} = default!;
                    {{this.FuncAssignStaticIndented("        ",predicate,nameof(predicate),this.temp,this.item)}}
                    if({{this.temp}}) count++;
                }
                return ({{resultType}})count;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}){
                {{resultType}} count = 0;
                checked{
                    while(source.TryGetNext(out var __mn19)){
                        var {{this.item}} = __mn19;
                        bool {{this.temp}} = default!;
                        {{this.FuncAssignStaticIndented("            ",predicate,nameof(predicate),this.temp,this.item)}}
                        if({{this.temp}}) count++;
                    }
                }
                return count;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
