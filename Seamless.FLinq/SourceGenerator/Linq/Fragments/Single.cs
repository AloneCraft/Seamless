namespace Seamless.FLinq.Linq.Fragments;

/*
Single は source.Single() を、0件/2件以上を検出する Compute に変換する。

入力例:
  source.Single()
  source.Single(predicate)

生成例:
  if(!source.MoveNext()) throw NoElements();
  var value = source.Current;
  if(source.MoveNext()) throw MoreThanOneElement();
  return value;

バリエーション:
  - span source は Length で 0/1/>1 を即判定する。
  - Range/Repeat は count 式だけで inline 判定できる。
  - predicate 付きは一致数を found flag で追跡し、2件目で例外にする。
*/
public class Single(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) => method.Parameters.Length == 0;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}";
    protected override string ComputeExpression => $"Compute({e})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        source is RangeRoot range
            ? RangePredicateOptimizer.RenderSingle(range,番号,destVar,destType)
        : source is RepeatRoot repeat
            ? RangePredicateOptimizer.RenderSingle(repeat,番号,destVar,destType)
            : RenderDirectEnumerableCallInline(source,destVar,destType,"Single")
        ?? RenderComputeInline(destVar,destType);
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source) {
                source.TryGetSpan(out var __span);
                if(__span.Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                if(__span.Length>1) throw new global::System.InvalidOperationException("Sequence contains more than one element");
                return __span[0];
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source) {
                if(!source.MoveNext()) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                {{resultType}} value = source.Current;
                if(source.MoveNext()) throw new global::System.InvalidOperationException("Sequence contains more than one element");
                return value;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}

public class Single_predicate(int 番号, Enumerable source, FuncFragment predicate, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length > 0 && method.Parameters[0].Type.TypeKind == TypeKind.Delegate;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(predicate, nameof(predicate));
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
            ? RangePredicateOptimizer.RenderSingle(range,predicate,番号,destVar,destType)
                ?? (TryRenderDirectLambda(predicate) is { } rangeLambda
                    ? RenderDirectEnumerableCallInline(source,destVar,destType,"Single",rangeLambda)
                    : RenderComputeInline(destVar,destType))
        : TryRenderDirectLambda(predicate) is { } lambda
            ? RenderDirectEnumerableCallInline(source,destVar,destType,"Single",lambda)
            : RenderComputeInline(destVar,destType);
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(predicate)}}) {
                {{resultType}} value = default!;
                bool found = false;
                source.TryGetSpan(out var __span);
                foreach(var {{item}} in __span){
                    bool {{temp}}=default!;
                    {{FuncAssignStaticIndented("        ",predicate, nameof(predicate),temp,item)}}
                    if({{temp}}) {
                        if(found) throw new global::System.InvalidOperationException("Sequence contains more than one matching element");
                        value={{item}};
                        found=true;
                    }
                }
                if(!found) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                return value;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(predicate)}}) {
                {{resultType}} value = default!;
                bool found = false;
                while(source.MoveNext()) {
                    var {{item}} = source.Current;
                    bool {{temp}} = default!;
                    {{FuncAssignStaticIndented("        ",predicate, nameof(predicate),temp,item)}}
                    if({{temp}}) {
                        if(found) throw new global::System.InvalidOperationException("Sequence contains more than one matching element");
                        value = {{item}};
                        found = true;
                    }
                }
                if(!found) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                return value;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
