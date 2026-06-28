namespace Seamless.FLinq.Linq.Fragments;
/*
Last は source.Last() を、最後に見た値を保持する Compute に変換する。

入力例:
  source.Last()
  source.Last(predicate)

生成例:
  found = false;
  while(source.MoveNext()) {
      found = true;
      result = source.Current;
  }
  if(!found) throw NoElements();
  return result;

バリエーション:
  - span source は span[^1] を返す。
  - RangeRoot<int> は start + count - 1 を inline する。
  - predicate 付き RangeRoot<int> は末尾側から逆向き loop を inline できる。
*/
public class Last(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) => method.Parameters.Length == 0;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}";
    protected override string ComputeExpression => $"Compute({e})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        RenderRangeLastInline(destVar,destType)
        ?? RenderDirectSpanLastInline(destVar,destType)
        ?? RenderDirectEnumerableCallInline(source,destVar,destType,"Last")
        ?? RenderComputeInline(destVar,destType);
    private string? RenderDirectSpanLastInline(string destVar,string destType) {
        var span=$"__span{result}";
        return RenderSpanInline(source,destVar,destType,span,spanName=>$$"""
            if({{spanName}}.Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
            {{destVar}}={{spanName}}[{{spanName}}.Length-1];
            """);
    }
    private string? RenderRangeLastInline(string destVar,string destType) {
        if(source is not RangeRoot range || source.ElementType!="int") {
            return null;
        }
        var start=$"__start{番号}";
        var count=$"__count{番号}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{start}}={{range.StartExpression}};
                var {{count}}={{range.CountExpression}};
                if({{count}}==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                {{destVar}}={{start}} + {{count}} - 1;
            }
            """;
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source) {
                source.TryGetSpan(out var __span);
                if(__span.Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                return __span[__span.Length-1];
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source) {
                var found=false;
                {{resultType}} result=default!;
                while(source.MoveNext()){
                    found=true;
                    result=source.Current;
                }
                if(!found) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                return result;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}

public class Last_predicate(int 番号, Enumerable source, FuncFragment predicate, string resultType) : BufferedScalar(番号, resultType) {
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
        RenderRangeLastPredicateInline(destVar,destType)
        ?? (TryRenderDirectLambda(predicate) is { } lambda
            ? RenderDirectEnumerableCallInline(source,destVar,destType,"Last",lambda)
            : RenderComputeInline(destVar,destType));
    private string? RenderRangeLastPredicateInline(string destVar,string destType) {
        if(source is not RangeRoot range
            || source.ElementType!="int"
            || !predicate.CanInlineAssign
            || predicate.RequiresStatementInlining) {
            return null;
        }
        var start=$"__start{番号}";
        var count=$"__count{番号}";
        var index=$"__i{番号}";
        var found=$"__found{番号}";
        return $$"""
            {{destType}} {{destVar}}=default!;
            {
                var {{start}}={{range.StartExpression}};
                var {{count}}={{range.CountExpression}};
                var {{found}}=false;
                for(var {{index}}={{count}}-1;{{index}}>=0;{{index}}--) {
                    var {{item}}={{start}} + {{index}};
                    bool {{temp}}=default!;
            {{IndentLines(FuncAssignStatic(predicate,nameof(predicate),temp,item),"        ")}}
                    if({{temp}}) {
                        {{destVar}}={{item}};
                        {{found}}=true;
                        break;
                    }
                }
                if(!{{found}}) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
            }
            """;
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(predicate)}}) {
                var found=false;
                {{resultType}} result=default!;
                source.TryGetSpan(out var __span);
                foreach(var {{item}} in __span){
                    bool {{temp}}=default!;
                    {{FuncAssignStaticIndented("        ",predicate,nameof(predicate),temp,item)}}
                    if({{temp}}){ found=true; result={{item}}; }
                }
                if(!found) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                return result;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(predicate)}}) {
                var found=false;
                {{resultType}} result=default!;
                while(source.MoveNext()){
                    var {{item}}=source.Current;
                    bool {{temp}}=default!;
                    {{FuncAssignStaticIndented("        ",predicate,nameof(predicate),temp,item)}}
                    if({{temp}}){ found=true; result={{item}}; }
                }
                if(!found) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                return result;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
