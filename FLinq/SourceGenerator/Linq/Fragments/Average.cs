namespace Seamless.FLinq.Linq.Fragments;

public class Average(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    private bool UseDecimalAccumulator => source.ElementType=="decimal";
    private bool UseDoubleAccumulator =>
        source.ElementType=="int"
        || source.ElementType=="long"
        || source.ElementType=="float"
        || source.ElementType=="double";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => $"Compute({this.e})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderDirectEnumerableCallInline(source,destVar,destType,"Average")
        ?? this.RenderComputeInline(destVar,destType);
    protected override string ComputeMembers => this.BuildComputeMembers(source.ElementType,"item","source.Current");
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";

    private string BuildComputeMembers(string valueType,string spanValue,string enumValue) {
        if(this.UseDecimalAccumulator) {
            return source.CanProvideSpan
                ? $$"""
                    {{AggressiveInliningAttribute}}
                    private static {{resultType}} Compute(TEnum source) {
                        var sum=0m;
                        source.TryGetSpan(out var __span);
                        if(__span.Length==0) throw global::Seamless.Collections.Tsavorite.GeneratedCodeHelpers.NoElements();
                        foreach(var item in __span) sum+=item;
                        return ({{resultType}})(sum/__span.Length);
                    }
                    """
                : $$"""
                    {{AggressiveInliningAttribute}}
                    private static {{resultType}} Compute(TEnum source) {
                        long count=0;
                        var sum=0m;
                        while(source.MoveNext()){
                            sum+=source.Current;
                            count++;
                        }
                        if(count==0) throw global::Seamless.Collections.Tsavorite.GeneratedCodeHelpers.NoElements();
                        return ({{resultType}})(sum/count);
                    }
                    """;
        }
        if(this.UseDoubleAccumulator) {
            return source.CanProvideSpan
                ? source.ElementType=="int"
                    ? $$"""
                        {{AggressiveInliningAttribute}}
                        private static {{resultType}} Compute(TEnum source) {
                            source.TryGetSpan(out var __span);
                            if(__span.Length==0) throw global::Seamless.Collections.Tsavorite.GeneratedCodeHelpers.NoElements();
                            return ({{resultType}})global::Seamless.Collections.Tsavorite.Carrier.ValueEnumerableExtensions.AverageIntSimd(__span);
                        }
                        """
                    : $$"""
                        {{AggressiveInliningAttribute}}
                        private static {{resultType}} Compute(TEnum source) {
                            double sum=0;
                            source.TryGetSpan(out var __span);
                            if(__span.Length==0) throw global::Seamless.Collections.Tsavorite.GeneratedCodeHelpers.NoElements();
                            foreach(var item in __span) sum+=item;
                            return ({{resultType}})(sum/__span.Length);
                        }
                        """
                : $$"""
                    {{AggressiveInliningAttribute}}
                    private static {{resultType}} Compute(TEnum source) {
                        long count=0;
                        double sum=0;
                        while(source.MoveNext()){
                            sum+=source.Current;
                            count++;
                        }
                        if(count==0) throw global::Seamless.Collections.Tsavorite.GeneratedCodeHelpers.NoElements();
                        return ({{resultType}})(sum/count);
                    }
                    """;
        }
        return source.CanProvideSpan
            ? $$"""
                {{AggressiveInliningAttribute}}
                private static {{resultType}} Compute(TEnum source) {
                    long count=0;
                    {{resultType}} sum=default!;
                    source.TryGetSpan(out var __span);
                    if(__span.Length==0) throw global::Seamless.Collections.Tsavorite.GeneratedCodeHelpers.NoElements();
                    foreach(var item in __span){
                        sum+=item;
                        count++;
                    }
                    return sum/({{resultType}})count;
                }
                """
            : $$"""
                {{AggressiveInliningAttribute}}
                private static {{resultType}} Compute(TEnum source) {
                    long count=0;
                    {{resultType}} sum=default!;
                    while(source.MoveNext()){
                        sum+=source.Current;
                        count++;
                    }
                    if(count==0) throw global::Seamless.Collections.Tsavorite.GeneratedCodeHelpers.NoElements();
                    return sum/({{resultType}})count;
                }
                """;
    }
}

public class Average_selector(int 番号, Enumerable source, FuncFragment selector, string resultType) : BufferedScalar(番号, resultType) {
    private string SelectorFuncType => $"global::System.Func<{source.ElementType}, {FuncReturnTypeName(selector)}>";
    private bool UseDecimalAccumulator => FuncReturnTypeName(selector)=="decimal";
    private bool UseDoubleAccumulator =>
        FuncReturnTypeName(selector)=="int"
        || FuncReturnTypeName(selector)=="long"
        || FuncReturnTypeName(selector)=="float"
        || FuncReturnTypeName(selector)=="double";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(selector,nameof(selector));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(selector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(selector)}";
    protected override string ComputeExpression =>
        $"Compute({this.e}{this.ClosureArgsSuffix(selector)})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderComputeInline(destVar,destType)
        ?? (TryRenderDirectLambda(selector) is { } lambda
            ? this.RenderDirectEnumerableCallInline(source,destVar,destType,"Average",lambda)
            : null);
    protected override string ComputeMembers => this.BuildComputeMembers();
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";

    private string BuildComputeMembers() {
        if(this.UseDecimalAccumulator) {
            return source.CanProvideSpan
                ? $$"""
                    {{AggressiveInliningAttribute}}
                    private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(selector)}}) {
                        var sum=0m;
                        source.TryGetSpan(out var __span);
                        if(__span.Length==0) throw global::Seamless.Collections.Tsavorite.GeneratedCodeHelpers.NoElements();
                        foreach(var {{this.item}} in __span){
                            {{FuncReturnTypeName(selector)}} {{this.temp}}=default!;
                            {{this.FuncAssignStaticIndented("        ",selector,nameof(selector),this.temp,this.item)}}
                            sum+={{this.temp}};
                        }
                        return ({{resultType}})(sum/__span.Length);
                    }
                    """
                : $$"""
                    {{AggressiveInliningAttribute}}
                    private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(selector)}}) {
                        long count=0;
                        var sum=0m;
                        while(source.MoveNext()){
                            var {{this.item}}=source.Current;
                            {{FuncReturnTypeName(selector)}} {{this.temp}}=default!;
                            {{this.FuncAssignStaticIndented("        ",selector,nameof(selector),this.temp,this.item)}}
                            sum+={{this.temp}};
                            count++;
                        }
                        if(count==0) throw global::Seamless.Collections.Tsavorite.GeneratedCodeHelpers.NoElements();
                        return ({{resultType}})(sum/count);
                    }
                    """;
        }
        if(this.UseDoubleAccumulator) {
            return source.CanProvideSpan
                ? $$"""
                    {{AggressiveInliningAttribute}}
                    private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(selector)}}) {
                        double sum=0;
                        source.TryGetSpan(out var __span);
                        if(__span.Length==0) throw global::Seamless.Collections.Tsavorite.GeneratedCodeHelpers.NoElements();
                        foreach(var {{this.item}} in __span){
                            {{FuncReturnTypeName(selector)}} {{this.temp}}=default!;
                            {{this.FuncAssignStaticIndented("        ",selector,nameof(selector),this.temp,this.item)}}
                            sum+={{this.temp}};
                        }
                        return ({{resultType}})(sum/__span.Length);
                    }
                    """
                : $$"""
                    {{AggressiveInliningAttribute}}
                    private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(selector)}}) {
                        long count=0;
                        double sum=0;
                        while(source.MoveNext()){
                            var {{this.item}}=source.Current;
                            {{FuncReturnTypeName(selector)}} {{this.temp}}=default!;
                            {{this.FuncAssignStaticIndented("        ",selector,nameof(selector),this.temp,this.item)}}
                            sum+={{this.temp}};
                            count++;
                        }
                        if(count==0) throw global::Seamless.Collections.Tsavorite.GeneratedCodeHelpers.NoElements();
                        return ({{resultType}})(sum/count);
                    }
                    """;
        }
        return source.CanProvideSpan
            ? $$"""
                {{AggressiveInliningAttribute}}
                private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(selector)}}) {
                    long count=0;
                    {{resultType}} sum=default!;
                    source.TryGetSpan(out var __span);
                    if(__span.Length==0) throw global::Seamless.Collections.Tsavorite.GeneratedCodeHelpers.NoElements();
                    foreach(var {{this.item}} in __span){
                        {{FuncReturnTypeName(selector)}} {{this.temp}}=default!;
                        {{this.FuncAssignStaticIndented("        ",selector,nameof(selector),this.temp,this.item)}}
                        sum+={{this.temp}};
                        count++;
                    }
                    return sum/({{resultType}})count;
                }
                """
            : $$"""
                {{AggressiveInliningAttribute}}
                private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(selector)}}) {
                    long count=0;
                    {{resultType}} sum=default!;
                    while(source.MoveNext()){
                        var {{this.item}}=source.Current;
                        {{FuncReturnTypeName(selector)}} {{this.temp}}=default!;
                        {{this.FuncAssignStaticIndented("        ",selector,nameof(selector),this.temp,this.item)}}
                        sum+={{this.temp}};
                        count++;
                    }
                    if(count==0) throw global::Seamless.Collections.Tsavorite.GeneratedCodeHelpers.NoElements();
                    return sum/({{resultType}})count;
                }
                """;
    }
}
