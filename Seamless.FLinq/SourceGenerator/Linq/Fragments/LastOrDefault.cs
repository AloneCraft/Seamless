namespace Seamless.FLinq.Linq.Fragments;
public class LastOrDefault(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) => method.Parameters.Length == 0;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}";
    protected override string ComputeExpression => $"Compute({e})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        RenderRangeLastOrDefaultInline(destVar,destType)
        ?? RenderDirectSpanLastOrDefaultInline(destVar,destType)
        ?? RenderDirectEnumerableCallInline(source,destVar,destType,"LastOrDefault")
        ?? RenderComputeInline(destVar,destType);
    private string? RenderDirectSpanLastOrDefaultInline(string destVar,string destType) {
        var span=$"__span{result}";
        return RenderSpanInline(source,destVar,destType,span,spanName=>$$"""
            {{destVar}}={{spanName}}.Length==0 ? default! : {{spanName}}[{{spanName}}.Length-1];
            """);
    }
    private string? RenderRangeLastOrDefaultInline(string destVar,string destType) {
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
                {{destVar}}={{count}}>0 ? {{start}} + {{count}} - 1 : default!;
            }
            """;
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source) {
                source.TryGetSpan(out var __span);
                return __span.Length>0?__span[__span.Length-1]:default!;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source) {
                {{resultType}} result=default!;
                while(source.MoveNext()) result=source.Current;
                return result;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}

public class LastOrDefault_defaultValue(int 番号, Enumerable source, string defaultValue, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 1 && method.Parameters[0].Type.TypeKind != TypeKind.Delegate;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {defaultValue})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}, {resultType} defaultValue";
    protected override string ComputeExpression => $"Compute({e}, defaultValue)";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        RenderRangeLastOrDefaultDefaultValueInline(destVar,destType)
        ?? RenderDirectSpanLastOrDefaultDefaultValueInline(destVar,destType)
        ?? RenderDirectEnumerableCallInline(source,destVar,destType,"LastOrDefault",defaultValue)
        ?? RenderComputeInline(destVar,destType);
    private string? RenderDirectSpanLastOrDefaultDefaultValueInline(string destVar,string destType) {
        var span=$"__span{result}";
        return RenderSpanInline(source,destVar,destType,span,spanName=>$$"""
            {{destVar}}={{spanName}}.Length==0 ? {{defaultValue}} : {{spanName}}[{{spanName}}.Length-1];
            """);
    }
    private string? RenderRangeLastOrDefaultDefaultValueInline(string destVar,string destType) {
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
                {{destVar}}={{count}}>0 ? {{start}} + {{count}} - 1 : {{defaultValue}};
            }
            """;
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source, {{resultType}} defaultValue) {
                source.TryGetSpan(out var __span);
                return __span.Length>0?__span[__span.Length-1]:defaultValue;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source, {{resultType}} defaultValue) {
                {{resultType}} result = defaultValue;
                while(source.MoveNext()) result = source.Current;
                return result;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}

public class LastOrDefault_predicate(int 番号, Enumerable source, FuncFragment predicate, string resultType) : BufferedScalar(番号, resultType) {
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
        RenderRangeLastOrDefaultPredicateInline(destVar,destType)
        ?? (TryRenderDirectLambda(predicate) is { } lambda
            ? RenderDirectEnumerableCallInline(source,destVar,destType,"LastOrDefault",lambda)
            : RenderComputeInline(destVar,destType));
    private string? RenderRangeLastOrDefaultPredicateInline(string destVar,string destType) {
        if(source is not RangeRoot range
            || source.ElementType!="int"
            || !predicate.CanInlineAssign
            || predicate.RequiresStatementInlining) {
            return null;
        }
        var start=$"__start{番号}";
        var count=$"__count{番号}";
        var index=$"__i{番号}";
        return $$"""
            {{destType}} {{destVar}}=default!;
            {
                var {{start}}={{range.StartExpression}};
                var {{count}}={{range.CountExpression}};
                for(var {{index}}={{count}}-1;{{index}}>=0;{{index}}--) {
                    var {{item}}={{start}} + {{index}};
                    bool {{temp}}=default!;
            {{IndentLines(FuncAssignStatic(predicate,nameof(predicate),temp,item),"        ")}}
                    if({{temp}}) {
                        {{destVar}}={{item}};
                        break;
                    }
                }
            }
            """;
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(predicate)}}) {
                {{resultType}} result = default!;
                source.TryGetSpan(out var __span);
                for(var __i=__span.Length-1;__i>=0;__i--){
                    var {{item}}=__span[__i];
                    bool {{temp}}=default!;
                    {{FuncAssignStaticIndented("        ",predicate,nameof(predicate),temp,item)}}
                    if({{temp}}){
                        return {{item}};
                    }
                }
                return result;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(predicate)}}) {
                {{resultType}} result = default!;
                while(source.MoveNext()) {
                    var {{item}} = source.Current;
                    bool {{temp}} = default!;
                    {{FuncAssignStaticIndented("        ",predicate, nameof(predicate),temp,item)}}
                    if({{temp}}) result = {{item}};
                }
                return result;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
