namespace Seamless.FLinq.Linq.Fragments;

/*
First は source.First() を、最初の1要素だけ読む Compute に変換する。

入力例:
  source.First()
  source.First(predicate)

生成例:
  if(source.MoveNext()) return source.Current;
  throw NoElements();

バリエーション:
  - RangeRoot は start 式だけで inline できる。
  - predicate 付きは predicate(item) が true の最初の item を返す。
  - predicate が直接 lambda 化できる場合は DirectEnumerableExpression 経由で System.Linq.First へ委譲できる。
*/
public class First(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) => method.Parameters.Length == 0;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}";
    protected override string ComputeExpression => $"Compute({e})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        source is RangeRoot range
            ? RangePredicateOptimizer.RenderFirst(range,番号,destVar,destType)
        : RenderDirectEnumerableCallInline(source,destVar,destType,"First")
        ?? RenderComputeInline(destVar,destType);
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source) {
            if(source.MoveNext()) return source.Current;
            throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}

public class First_predicate(int 番号, Enumerable source, FuncFragment predicate, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length > 0 && method.Parameters[0].Type.TypeKind == TypeKind.Delegate;
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
        TryRenderDirectLambda(predicate) is { } lambda
            ? RenderDirectEnumerableCallInline(source,destVar,destType,"First",lambda)
            : RenderComputeInline(destVar,destType);
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(predicate)}}) {
            while(source.MoveNext()) {
                var {{item}} = source.Current;
                bool {{temp}} = default!;
                {{FuncAssignStaticIndented("        ",predicate,nameof(predicate),temp,item)}}
                if({{temp}}) return {{item}};
            }
            throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
