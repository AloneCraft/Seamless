namespace Seamless.FLinq.Linq.Fragments;

/*
Count は Count/LongCount を件数取得 fast path 優先の Compute へ変換する。

入力例:
  source.Count()
  source.LongCount()

生成例:
  if(source.TryGetNonEnumeratedCount(out var count)) return (TResult)count;
  checked {
      while(source.MoveNext()) count++;
  }

バリエーション:
  - Range/Repeat は count 式をそのまま resultType へ cast して inline する。
  - source が span を確実に返せる場合は span.Length を返す。
  - LongCount では TryGetNonEnumeratedCount が int なので long へ拡張する。
*/
public class Count(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}";
    protected override string ComputeExpression => $"Compute({e})";
    public override string? RenderInlineComputation(string destVar,string destType) {
        if(source is RangeRoot range) {
            return $"{destType} {destVar}=({destType})({range.CountExpression});";
        }
        if(source is RepeatRoot repeat) {
            return $"{destType} {destVar}=({destType})({repeat.CountExpression});";
        }
        if(source.DirectEnumerableExpression is not { } sourceExpression) {
            return RenderComputeInline(destVar,destType);
        }
        if(resultType is not ("long" or "global::System.Int64")) {
            return RenderDirectEnumerableCallInline(source,destVar,destType,"Count");
        }
        var sourceVar=$"__src{result}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{sourceVar}}={{sourceExpression}};
                if(global::System.Linq.Enumerable.TryGetNonEnumeratedCount({{sourceVar}}, out var __count)) {
                    {{destVar}}=({{resultType}})__count;
                }else{
                    {{destVar}}=global::System.Linq.Enumerable.LongCount({{sourceVar}});
                }
            }
            """;
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source) {
                source.TryGetSpan(out var __span);
                return ({{resultType}})__span.Length;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source) {
                if(source.TryGetNonEnumeratedCount(out var __count)) {
                    return ({{resultType}})__count;
                }
                {{resultType}} count=0;
                checked{
                    while(source.MoveNext()) count++;
                }
                return count;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
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
  - predicate が直接 lambda として再表示できる場合は DirectEnumerableExpression 経由で System.Linq へ委譲できる。
*/
public class Count_predicate(int 番号, Enumerable source, FuncFragment predicate, string resultType) : BufferedScalar(番号, resultType) {
    private string PredicateFuncType => $"global::System.Func<{source.ElementType}, bool>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(predicate,nameof(predicate));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(predicate)}";
    protected override string ComputeExpression =>
        $"Compute({e}{ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorConstructor =>
        $"this.Current = Compute({e}{ClosureArgsSuffixForCtorBody(predicate)});";
    public override string? RenderInlineComputation(string destVar,string destType) {
        var methodName = resultType is "long" or "global::System.Int64" ? "LongCount" : "Count";
        return source is RangeRoot range
            ? RangePredicateOptimizer.RenderCount(range,predicate,番号,destVar,destType)
                ?? (TryRenderDirectLambda(predicate) is { } rangeLambda
                    ? RenderDirectEnumerableCallInline(source,destVar,destType,methodName,rangeLambda)
                    : RenderComputeInline(destVar,destType))
        : TryRenderDirectLambda(predicate) is { } lambda
            ? RenderDirectEnumerableCallInline(source,destVar,destType,methodName,lambda)
            : RenderComputeInline(destVar,destType);
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(predicate)}}) {
                source.TryGetSpan(out var __span);
                {{resultType}} count = 0;
                checked{
                    for(var __i = 0; __i < __span.Length; __i++) {
                        var {{item}} = __span[__i];
                        bool {{temp}} = default!;
                        {{FuncAssignStaticIndented("            ",predicate,nameof(predicate),temp,item)}}
                        if({{temp}}) count++;
                    }
                }
                return count;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(predicate)}}) {
                {{resultType}} count = 0;
                checked{
                    while(source.MoveNext()) {
                        var {{item}} = source.Current;
                        bool {{temp}} = default!;
                        {{FuncAssignStaticIndented("            ",predicate,nameof(predicate),temp,item)}}
                        if({{temp}}) count++;
                    }
                }
                return count;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
