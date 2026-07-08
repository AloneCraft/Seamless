namespace Seamless.FLinq.Linq.Fragments;
public class FirstOrDefault(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) => method.Parameters.Length == 0;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => $"Compute({this.e})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderRangeFirstOrDefaultInline(destVar,destType)
        ??this.RenderDirectSpanFirstOrDefaultInline(destVar,destType)
        ??this.RenderDirectEnumerableCallInline(source,destVar,destType,"FirstOrDefault")
        ??this.RenderComputeInline(destVar,destType);
    private string? RenderDirectSpanFirstOrDefaultInline(string destVar,string destType) {
        var span=$"__span{this.result}";
        return this.RenderSpanInline(source,destVar,destType,span,spanName=>$$"""
            {{destVar}}={{spanName}}.Length==0 ? default! : {{spanName}}[0];
            """);
    }
    private string? RenderRangeFirstOrDefaultInline(string destVar,string destType) {
        if(source is not RangeRoot range || source.ElementType!="int") {
            return null;
        }
        var start=$"__start{this.result}";
        var count=$"__count{this.result}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{start}}={{range.StartExpression}};
                var {{count}}={{range.CountExpression}};
                {{destVar}}={{count}}>0 ? {{start}} : default!;
            }
            """;
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source) {
                source.TryGetSpan(out var __span);
                return __span.Length>0?__span[0]:default!;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source) {
                return source.MoveNext() ? source.Current : default!;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

public class FirstOrDefault_defaultValue(int 番号, Enumerable source, string defaultValue, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 1 && method.Parameters[0].Type.TypeKind != TypeKind.Delegate;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {defaultValue})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}, {resultType} defaultValue";
    protected override string ComputeExpression => $"Compute({this.e}, defaultValue)";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderRangeFirstOrDefaultDefaultValueInline(destVar,destType)
        ??this.RenderDirectSpanFirstOrDefaultDefaultValueInline(destVar,destType)
        ??this.RenderDirectEnumerableCallInline(source,destVar,destType,"FirstOrDefault",defaultValue)
        ??this.RenderComputeInline(destVar,destType);
    private string? RenderDirectSpanFirstOrDefaultDefaultValueInline(string destVar,string destType) {
        var span=$"__span{this.result}";
        return this.RenderSpanInline(source,destVar,destType,span,spanName=>$$"""
            {{destVar}}={{spanName}}.Length==0 ? {{defaultValue}} : {{spanName}}[0];
            """);
    }
    private string? RenderRangeFirstOrDefaultDefaultValueInline(string destVar,string destType) {
        if(source is not RangeRoot range || source.ElementType!="int") {
            return null;
        }
        var start=$"__start{this.result}";
        var count=$"__count{this.result}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{start}}={{range.StartExpression}};
                var {{count}}={{range.CountExpression}};
                {{destVar}}={{count}}>0 ? {{start}} : {{defaultValue}};
            }
            """;
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source, {{resultType}} defaultValue) {
                source.TryGetSpan(out var __span);
                return __span.Length>0?__span[0]:defaultValue;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source, {{resultType}} defaultValue) {
                return source.MoveNext() ? source.Current : defaultValue;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

public class FirstOrDefault_predicate(int 番号, Enumerable source, FuncFragment predicate, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length > 0 && method.Parameters[0].Type.TypeKind == TypeKind.Delegate;
    private string PredicateFuncType => $"global::System.Func<{source.ElementType}, bool>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(predicate,nameof(predicate));
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
        this.RenderRangeFirstOrDefaultPredicateInline(destVar,destType)
        ??this.RenderDirectSpanFirstOrDefaultPredicateInline(destVar,destType)
        ??(TryRenderDirectLambda(predicate) is { } lambda
            ? this.RenderDirectEnumerableCallInline(source,destVar,destType,"FirstOrDefault",lambda)
            : this.RenderComputeInline(destVar,destType));
    private string? RenderDirectSpanFirstOrDefaultPredicateInline(string destVar,string destType) {
        if(!predicate.CanInlineAssign||predicate.RequiresStatementInlining) {
            return null;
        }
        var span=$"__span{this.result}";
        var index=$"__i{this.result}";
        return this.RenderSpanInline(source,destVar,destType,span,spanName=>$$"""
            {{destVar}}=default!;
            for(var {{index}}=0;{{index}}<{{spanName}}.Length;{{index}}++) {
                var {{this.item}}={{spanName}}[{{index}}];
                bool {{this.temp}}=default!;
            {{IndentLines(this.FuncAssignStatic(predicate,nameof(predicate),this.temp,this.item),"        ")}}
                if({{this.temp}}) {
                    {{destVar}}={{this.item}};
                    break;
                }
            }
            """);
    }
    private string? RenderRangeFirstOrDefaultPredicateInline(string destVar,string destType) {
        if(source is not RangeRoot range
            || source.ElementType!="int"
            || !predicate.CanInlineAssign
            || predicate.RequiresStatementInlining) {
            return null;
        }
        var start=$"__start{this.result}";
        var count=$"__count{this.result}";
        var index=$"__i{this.result}";
        return $$"""
            {{destType}} {{destVar}}=default!;
            {
                var {{start}}={{range.StartExpression}};
                var {{count}}={{range.CountExpression}};
                for(var {{index}}=0;{{index}}<{{count}};{{index}}++) {
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
                source.TryGetSpan(out var __span);
                foreach(var {{this.item}} in __span) {
                    bool {{this.temp}} = default!;
                    {{this.FuncAssignStaticIndented("        ",predicate,nameof(predicate),this.temp,this.item)}}
                    if({{this.temp}}) return {{this.item}};
                }
                return default!;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}) {
                while(source.MoveNext()) {
                    var {{this.item}} = source.Current;
                    bool {{this.temp}} = default!;
                    {{this.FuncAssignStaticIndented("        ",predicate,nameof(predicate),this.temp,this.item)}}
                    if({{this.temp}}) return {{this.item}};
                }
                return default!;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
