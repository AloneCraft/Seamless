namespace Seamless.FLinq.Generator;

internal class LastOrDefault(int 番号,Enumerable source,string resultType):BufferedScalar(番号,resultType) {
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
        this.RenderRangeLastOrDefaultInline(destVar,destType)
        ??this.RenderDirectSpanLastOrDefaultInline(destVar,destType)
        ??this.RenderConsumeBasedLastOrDefaultInline(destVar,destType)
        ??this.RenderDirectEnumerableCallInline(source,destVar,destType,"LastOrDefault")
        ??this.RenderComputeInline(destVar,destType);

    private string? RenderConsumeBasedLastOrDefaultInline(string destVar,string destType) {
        if(source is not (Join or Zip or Zip_resultSelector or GroupJoin or SelectManyBase)) {
            return null;
        }
        var placeholder = $"__FLINQ_LASTORDEFAULT_{番号}__";
        string consumeStatement;
        try {
            consumeStatement=source.GetConsumeExecutionStatement(placeholder);
        } catch(NotSupportedException) {
            return null;
        }
        if(string.IsNullOrWhiteSpace(consumeStatement)
            ||consumeStatement.Contains("TryGetNext")
            ||consumeStatement.Contains("GetRemainingEnumerator")
            ||!consumeStatement.Contains($"{placeholder}.Consume(")) {
            return null;
        }
        var escaped = global::System.Text.RegularExpressions.Regex.Escape(placeholder);
        var pattern = $@"(?<indent>[ \t]*){escaped}\.Consume\((?<arg>.+?)\);";
        var replaced = global::System.Text.RegularExpressions.Regex.Replace(consumeStatement,pattern,match => {
            var indent = match.Groups["indent"].Value;
            var arg = match.Groups["arg"].Value;
            return $"{indent}{destVar}={arg};";
        });
        return $$"""
            {{destType}} {{destVar}}=default!;
            {{replaced}}
            """;
    }

    private string? RenderDirectSpanLastOrDefaultInline(string destVar,string destType) {
        var span = $"__span{this.result}";
        return this.RenderSpanInline(source,destVar,destType,span,spanName => $$"""
            {{destVar}}={{spanName}}.Length==0 ? default! : {{spanName}}[{{spanName}}.Length-1];
            """);
    }
    private string? RenderRangeLastOrDefaultInline(string destVar,string destType) {
        if(source is not RangeRoot range||source.ElementType!="int") {
            return null;
        }
        var start = $"__start{番号}";
        var count = $"__count{番号}";
        return $$"""
            {{destType}} {{destVar}};
            {
                {{range.RenderArgumentPreparation(start,count)}}
                {{destVar}}={{count}}>0 ? {{start}} + {{count}} - 1 : default!;
            }
            """;
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(ref TEnum source){
                source.TryGetSpan(out var __span);
                return __span.Length>0?__span[__span.Length-1]:default!;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(ref TEnum source){
                {{resultType}} result=default!;
                while(source.TryGetNext(out var __mn41)) result=__mn41;
                return result;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

internal class LastOrDefault_defaultValue(int 番号,Enumerable source,string defaultValue,string resultType):BufferedScalar(番号,resultType) {
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
        this.RenderRangeLastOrDefaultDefaultValueInline(destVar,destType)
        ??this.RenderDirectSpanLastOrDefaultDefaultValueInline(destVar,destType)
        ??this.RenderComputeInline(destVar,destType,defaultValue);
    private string? RenderDirectSpanLastOrDefaultDefaultValueInline(string destVar,string destType) {
        var span = $"__span{this.result}";
        return this.RenderSpanInline(source,destVar,destType,span,spanName => $$"""
            {{destVar}}={{spanName}}.Length==0 ? {{defaultValue}} : {{spanName}}[{{spanName}}.Length-1];
            """);
    }
    private string? RenderRangeLastOrDefaultDefaultValueInline(string destVar,string destType) {
        if(source is not RangeRoot range||source.ElementType!="int") {
            return null;
        }
        var start = $"__start{番号}";
        var count = $"__count{番号}";
        return $$"""
            {{destType}} {{destVar}};
            {
                {{range.RenderArgumentPreparation(start,count)}}
                {{destVar}}={{count}}>0 ? {{start}} + {{count}} - 1 : {{defaultValue}};
            }
            """;
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(ref TEnum source, {{resultType}} defaultValue){
                source.TryGetSpan(out var __span);
                return __span.Length>0?__span[__span.Length-1]:defaultValue;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(ref TEnum source, {{resultType}} defaultValue){
                {{resultType}} result = defaultValue;
                while(source.TryGetNext(out var __mn42)) result = __mn42;
                return result;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

internal class LastOrDefault_predicate(int 番号,Enumerable source,FuncFragment predicate,string resultType):BufferedScalar(番号,resultType) {
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
        this.RenderRangeLastOrDefaultPredicateInline(destVar,destType)
        ??(TryRenderDirectLambda(predicate) is { } lambda
            ? this.RenderDirectEnumerableCallInline(source,destVar,destType,"LastOrDefault",lambda)
            : this.RenderComputeInline(destVar,destType));
    private string? RenderRangeLastOrDefaultPredicateInline(string destVar,string destType) {
        if(source is not RangeRoot range
            ||source.ElementType!="int"
            ||!predicate.CanInlineAssign
            ||predicate.RequiresStatementInlining) {
            return null;
        }
        var start = $"__start{番号}";
        var count = $"__count{番号}";
        var index = $"__i{番号}";
        return $$"""
            {{destType}} {{destVar}}=default!;
            {
                {{range.RenderArgumentPreparation(start,count)}}
                for(var {{index}}={{count}}-1;{{index}}>=0;{{index}}--){
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
            private static {{resultType}} Compute(ref TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}){
                {{resultType}} result = default!;
                while(source.TryGetNext(out var __mn43)){
                    var {{this.item}} = __mn43;
                    bool {{this.temp}} = default!;
                    {{this.FuncAssignStaticIndented("        ",predicate,nameof(predicate),this.temp,this.item)}}
                    if({{this.temp}}) result = {{this.item}};
                }
                return result;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
