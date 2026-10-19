namespace Seamless.FLinq.Generator;

internal class FirstOrDefault(int 番号,Enumerable source,string resultType):BufferedScalar(番号,resultType) {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
    }

    public static bool IsApplicableVariant(IMethodSymbol method) => method.Parameters.Length==0;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.EnumeratorTypeName,source.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => $"Compute(ref {this.e})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        source is Zip zip
            ? zip.RenderDirectFirstInline(destVar,destType,orDefault: true)
            : source is Zip_resultSelector zipSel
                ? zipSel.RenderDirectFirstInline(destVar,destType,orDefault: true)
                : this.RenderRangeFirstOrDefaultInline(destVar,destType)
        ??this.RenderDirectSpanFirstOrDefaultInline(destVar,destType)
        ??this.RenderDirectEnumerableCallInline(source,destVar,destType,"FirstOrDefault")
        ??this.RenderComputeInline(destVar,destType);
    private string? RenderDirectSpanFirstOrDefaultInline(string destVar,string destType) {
        var span = $"__span{this.result}";
        return this.RenderSpanInline(source,destVar,destType,span,spanName => $$"""
            {{destVar}}={{spanName}}.Length==0 ? default! : {{spanName}}[0];
            """);
    }
    private string? RenderRangeFirstOrDefaultInline(string destVar,string destType) {
        if(source is not RangeRoot range||source.ElementType!="int") {
            return null;
        }
        var start = $"__start{this.result}";
        var count = $"__count{this.result}";
        return $$"""
            {{destType}} {{destVar}};
            {
                {{range.RenderArgumentPreparation(start,count)}}
                {{destVar}}={{count}}>0 ? {{start}} : default!;
            }
            """;
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(ref TEnum source){
                source.TryGetSpan(out var __span);
                return __span.Length>0?__span[0]:default!;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(ref TEnum source){
                return source.TryGetNext(out var __mn32) ? __mn32 : default!;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

internal class FirstOrDefault_defaultValue(int 番号,Enumerable source,string defaultValue,string resultType):BufferedScalar(番号,resultType) {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
    }

    public static bool IsApplicableVariant(IMethodSymbol method) =>
        method.Parameters.Length==1&&method.Parameters[0].Type.TypeKind!=TypeKind.Delegate;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.EnumeratorTypeName,source.GetEnumeratorExpression,defaultValue);
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}, {resultType} defaultValue";
    protected override string ComputeExpression => $"Compute(ref {this.e}, defaultValue)";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderRangeFirstOrDefaultDefaultValueInline(destVar,destType)
        ??this.RenderDirectSpanFirstOrDefaultDefaultValueInline(destVar,destType)
        ??this.RenderComputeInline(destVar,destType,defaultValue);
    private string? RenderDirectSpanFirstOrDefaultDefaultValueInline(string destVar,string destType) {
        var span = $"__span{this.result}";
        return this.RenderSpanInline(source,destVar,destType,span,spanName => $$"""
            {{destVar}}={{spanName}}.Length==0 ? {{defaultValue}} : {{spanName}}[0];
            """);
    }
    private string? RenderRangeFirstOrDefaultDefaultValueInline(string destVar,string destType) {
        if(source is not RangeRoot range||source.ElementType!="int") {
            return null;
        }
        var start = $"__start{this.result}";
        var count = $"__count{this.result}";
        return $$"""
            {{destType}} {{destVar}};
            {
                {{range.RenderArgumentPreparation(start,count)}}
                {{destVar}}={{count}}>0 ? {{start}} : {{defaultValue}};
            }
            """;
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(ref TEnum source, {{resultType}} defaultValue){
                source.TryGetSpan(out var __span);
                return __span.Length>0?__span[0]:defaultValue;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(ref TEnum source, {{resultType}} defaultValue){
                return source.TryGetNext(out var __mn33) ? __mn33 : defaultValue;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

internal class FirstOrDefault_predicate(int 番号,Enumerable source,FuncFragment predicate,string resultType):BufferedScalar(番号,resultType) {
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

    public static bool IsApplicableVariant(IMethodSymbol method) =>
        method.Parameters.Length>0&&method.Parameters[0].Type.TypeKind==TypeKind.Delegate;
    private string PredicateFuncType => $"global::System.Func<{source.ElementType}, bool>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(predicate,nameof(predicate));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        RenderEnumeratorExpressionWithClosure(this.EnumeratorTypeName,new[] { predicate },source.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(predicate)}";
    protected override string ComputeExpression =>
        $"Compute(ref {this.e}{this.ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorConstructor =>
        $"this.Current = Compute(ref this.{this.e}{this.ClosureArgsSuffixForCtorBody(predicate)});";
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
        var span = $"__span{this.result}";
        var index = $"__i{this.result}";
        var hoistedDeclarations = this.HoistedLocalDeclarations(predicate);
        var hoistedDeclarationBlock = string.IsNullOrEmpty(hoistedDeclarations)
            ? string.Empty
            : hoistedDeclarations+"\n";
        return this.RenderSpanInline(source,destVar,destType,span,spanName => $$"""
            {{hoistedDeclarationBlock}}
            {{destVar}}=default!;
            for(var {{index}}=0;{{index}}<{{spanName}}.Length;{{index}}++){
                var {{this.item}}={{spanName}}[{{index}}];
                bool {{this.temp}}=default!;
            {{IndentLines(this.FuncAssignStatic(predicate,nameof(predicate),this.temp,this.item),"        ")}}
                if({{this.temp}}){
                    {{destVar}}={{this.item}};
                    break;
                }
            }
            """);
    }
    private string? RenderRangeFirstOrDefaultPredicateInline(string destVar,string destType) {
        if(source is not RangeRoot range
            ||source.ElementType!="int"
            ||!predicate.CanInlineAssign
            ||predicate.RequiresStatementInlining) {
            return null;
        }
        var start = $"__start{this.result}";
        var count = $"__count{this.result}";
        var index = $"__i{this.result}";
        return $$"""
            {{destType}} {{destVar}}=default!;
            {
                {{range.RenderArgumentPreparation(start,count)}}
                for(var {{index}}=0;{{index}}<{{count}};{{index}}++){
                    var {{this.item}}={{start}} + {{index}};
                    bool {{this.temp}}=default!;
            {{IndentLines(this.FuncAssignStatic(predicate,nameof(predicate),this.temp,this.item),"        ")}}
                    if({{this.temp}}){
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
            private static {{resultType}} Compute(ref TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}){
                source.TryGetSpan(out var __span);
                foreach(var {{this.item}} in __span){
                    bool {{this.temp}} = default!;
                    {{this.FuncAssignStaticIndented("        ",predicate,nameof(predicate),this.temp,this.item)}}
                    if({{this.temp}}) return {{this.item}};
                }
                return default!;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(ref TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}){
                while(source.TryGetNext(out var __mn34)){
                    var {{this.item}} = __mn34;
                    bool {{this.temp}} = default!;
                    {{this.FuncAssignStaticIndented("        ",predicate,nameof(predicate),this.temp,this.item)}}
                    if({{this.temp}}) return {{this.item}};
                }
                return default!;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
