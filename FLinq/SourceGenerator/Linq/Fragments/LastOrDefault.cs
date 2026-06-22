namespace Seamless.FLinq.Linq.Fragments;
public class LastOrDefault(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) => method.Parameters.Length == 0;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => $"Compute({this.e})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderRangeLastOrDefaultInline(destVar,destType)
        ?? this.RenderDirectEnumerableCallInline(source,destVar,destType,"LastOrDefault")
        ?? this.RenderComputeInline(destVar,destType);
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
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

public class LastOrDefault_defaultValue(int 番号, Enumerable source, string defaultValue, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 1 && method.Parameters[0].Type.TypeKind != TypeKind.Delegate;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {defaultValue})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}, {resultType} defaultValue";
    protected override string ComputeExpression => $"Compute({this.e}, defaultValue)";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderRangeLastOrDefaultDefaultValueInline(destVar,destType)
        ?? this.RenderDirectEnumerableCallInline(source,destVar,destType,"LastOrDefault",defaultValue)
        ?? this.RenderComputeInline(destVar,destType);
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
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

public class LastOrDefault_predicate(int 番号, Enumerable source, FuncFragment predicate, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length > 0 && method.Parameters[0].Type.TypeKind == TypeKind.Delegate;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(predicate, nameof(predicate));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(predicate)}";
    protected override string ComputeExpression =>
        $"Compute({this.e}{this.ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorConstructor =>
        $"this.Current = Compute({this.e}{this.ClosureArgsSuffixForCtorBody(predicate)});";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderRangeLastOrDefaultPredicateInline(destVar,destType)
        ?? (TryRenderDirectLambda(predicate) is { } lambda
            ? this.RenderDirectEnumerableCallInline(source,destVar,destType,"LastOrDefault",lambda)
            : this.RenderComputeInline(destVar,destType));
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
                    var {{this.item}}={{start}} + {{index}};
                    bool {{this.temp}}=default!;
            {{IndentLines(this.FuncAssignStatic(predicate,nameof(predicate),this.temp,this.item),"        ")}}
                    if({{this.temp}}) {
                        {{destVar}}={{this.item}};
                        break;
                    }
                }
            }
            """;
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}) {
                {{resultType}} result = default!;
                source.TryGetSpan(out var __span);
                for(var __i=__span.Length-1;__i>=0;__i--){
                    var {{this.item}}=__span[__i];
                    bool {{this.temp}}=default!;
                    {{this.FuncAssignStaticIndented("        ",predicate,nameof(predicate),this.temp,this.item)}}
                    if({{this.temp}}){
                        return {{this.item}};
                    }
                }
                return result;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}) {
                {{resultType}} result = default!;
                while(source.MoveNext()) {
                    var {{this.item}} = source.Current;
                    bool {{this.temp}} = default!;
                    {{this.FuncAssignStaticIndented("        ",predicate, nameof(predicate),this.temp,this.item)}}
                    if({{this.temp}}) result = {{this.item}};
                }
                return result;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
