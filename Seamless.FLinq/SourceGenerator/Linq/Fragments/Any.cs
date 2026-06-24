namespace Seamless.FLinq.Linq.Fragments;
/*
Any は source.Any() を「件数 fast path か MoveNext 1回」で判定する scalar に変換する。

入力例:
  source.Any()

生成例:
  if(source.TryGetNonEnumeratedCount(out var count)) result = count != 0;
  else result = source.MoveNext();

バリエーション:
  - Range/Repeat は count 式だけで inline 結果を出せる。
  - Any(predicate) は predicate が true になった時点で true を返す Compute を生成する。
  - Any(indexPredicate) は index を増やしながら predicate(item, index) を評価する。
*/
public class Any(int 番号,Enumerable source):Scalar(番号){
    internal static bool TryBuildWithPredicate(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        out Fragment fragment,
        out string staticMethods,
        List<string>? unsafeAccessorCollector,
        BuildEnumerableFragmentDelegate buildEnumerable,
        BuildFuncFragmentDelegate buildFunc,
        CreateFragmentDelegate createFragment) {
        fragment = null!;
        staticMethods = string.Empty;
        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
            return false;
        ExpressionSyntax sourceExpression;
        var argumentStartIndex = 0;
        if(method.ReducedFrom is not null
            && invocation.Expression is MemberAccessExpressionSyntax { Expression: var receiverExpression }) {
            sourceExpression = receiverExpression;
        } else {
            var definition = method.ReducedFrom ?? method;
            if(definition.ContainingType.ToDisplayString() != "System.Linq.Enumerable"
                || definition.Parameters.Length == 0
                || invocation.ArgumentList.Arguments.Count == 0) {
                return false;
            }
            sourceExpression = invocation.ArgumentList.Arguments[0].Expression;
            argumentStartIndex = 1;
        }
        if(!buildEnumerable(semanticModel, sourceExpression, containingType, ref uniqueNumber, out var sourceFragment, out var sourceStaticMethods, unsafeAccessorCollector))
            return false;
        if(invocation.ArgumentList.Arguments.Count - argumentStartIndex == 1) {
            if(!buildFunc(invocation.ArgumentList.Arguments[argumentStartIndex].Expression, method.Parameters[argumentStartIndex].Type, ref uniqueNumber, out var predicate, semanticModel, containingType))
                return false;
            var anyVariantName = predicate.ParameterTypeNames.Length > 1 ? "indexPredicate" : "predicate";
            var anyPredicateFragment = createFragment("Any", anyVariantName, uniqueNumber++, [sourceFragment, predicate]);
            if(anyPredicateFragment is null)
                return false;
            fragment = anyPredicateFragment;
            staticMethods = JoinStaticMethods(sourceStaticMethods, fragment.StructMethod);
            return true;
        } else if(invocation.ArgumentList.Arguments.Count != argumentStartIndex) {
            return false;
        }
        var anyFragment = createFragment("Any", string.Empty, uniqueNumber++, [sourceFragment]);
        if(anyFragment is null)
            return false;
        fragment = anyFragment;
        staticMethods = JoinStaticMethods(sourceStaticMethods, fragment.StructMethod);
        return true;
    }
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string InlineSupportingMembers => source.GetEnumeratorStruct;
    public override string? RenderInlineComputation(string destVar,string destType) =>
        source is RangeRoot range
            ? RangePredicateOptimizer.RenderAny(range,番号,destVar,destType)
        : source is RepeatRoot repeat
            ? RangePredicateOptimizer.RenderAny(repeat,番号,destVar,destType)
        : source.NonEnumeratedCountExpression is { } countExpression
            ? $"{destType} {destVar}=({countExpression})!=0;"
        : RenderDirectEnumerableCallInline(source,destVar,destType,"Any")
            ?? RenderEnumeratorInline(destVar,destType);
    private string RenderEnumeratorInline(string destVar,string destType) {
        var enumeratorName=$"__s{番号}";
        return $$"""
            {{destType}} {{destVar}};
            using(var {{enumeratorName}}={{source.GetEnumeratorExpression}}) {
                if({{enumeratorName}}.TryGetNonEnumeratedCount(out var __count{{番号}})) {
                    {{destVar}}=__count{{番号}}!=0;
                }else{
                    {{destVar}}={{enumeratorName}}.TryGetNext(out _);
                }
            }
            """;
    }
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{DefineScalarEnumeratorStruct("bool")}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}";
    public override string GetEnumeratorFields => $$"""
        private bool {{yielded}} = false;
        """;
    public override string GetTryGetNextBody => $$"""
        if(this.{{yielded}}) {
            current = default;
            return false;
        }
        this.{{yielded}} = true;
        if({{e}}.TryGetNonEnumeratedCount(out var __count)) {
            this.{{currentValue}} = __count != 0;
        }else{
            this.{{currentValue}} = {{e}}.MoveNext();
        }
        current = this.{{currentValue}};
        return true;
        """;
    public override string GetCurrentExpression => currentValue;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}

/*
Any_predicate は source.Any(predicate) を、最初に true になった要素で短絡する Compute にする。

生成例:
  foreach(var item in source) {
      if(predicate(item)) return true;
  }
  return false;

source が span を確実に返せる場合は foreach(span) を生成し、それ以外は MoveNext loop にする。
*/
public class Any_predicate(int 番号, Enumerable source, FuncFragment predicate) : BufferedScalar(番号, "bool") {
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
    public override string? RenderInlineComputation(string destVar,string destType) =>
        source is RangeRoot range
            ? RangePredicateOptimizer.RenderAny(range,predicate,番号,destVar,destType)
                ?? (TryRenderDirectLambda(predicate) is { } rangeLambda
                    ? RenderDirectEnumerableCallInline(source,destVar,destType,"Any",rangeLambda) ?? RenderComputeInline(destVar,destType)
                    : RenderComputeInline(destVar,destType))
        : TryRenderDirectLambda(predicate) is { } lambda
            ? RenderDirectEnumerableCallInline(source,destVar,destType,"Any",lambda) ?? RenderComputeInline(destVar,destType)
            : RenderComputeInline(destVar,destType);
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static bool Compute(TEnum source{{ClosureMethodParameterSuffix(predicate)}}) {
                source.TryGetSpan(out var __span);
                foreach(var {{item}} in __span){
                    bool {{temp}}=default!;
                    {{FuncAssignStaticIndented("        ",predicate,nameof(predicate),temp,item)}}
                    if({{temp}}) return true;
                }
                return false;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static bool Compute(TEnum source{{ClosureMethodParameterSuffix(predicate)}}) {
                while(source.MoveNext()) {
                    var {{item}}=source.Current;
                    bool {{temp}}=default!;
                    {{FuncAssignStaticIndented("        ",predicate,nameof(predicate),temp,item)}}
                    if({{temp}}) return true;
                }
                return false;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}

public class Any_indexPredicate(int 番号, Enumerable source, FuncFragment predicate) : BufferedScalar(番号, "bool") {
    private string PredicateFuncType => $"global::System.Func<{source.ElementType}, int, bool>";
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
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static bool Compute(TEnum source{{ClosureMethodParameterSuffix(predicate)}}) {
            var {{a}}=0;
            while(source.MoveNext()) {
                var {{item}}=source.Current;
                bool {{temp}}=default!;
                {{FuncAssignStaticIndented("        ",predicate,nameof(predicate),temp,item,a)}}
                if({{temp}}) return true;
                {{a}}++;
            }
            return false;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
