namespace Seamless.FLinq.Generator;

internal class ElementAt(int 番号,Enumerable source,string index,string resultType):BufferedScalar(番号,resultType) {
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
        method.Parameters.Length==1&&method.Parameters[0].Type.SpecialType==SpecialType.System_Int32;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.EnumeratorTypeName,source.GetEnumeratorExpression,index);
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}, int index";
    protected override string ComputeExpression => $"Compute(ref {this.e}, index)";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderRangeElementAtInline(destVar,destType)
        ??this.RenderDirectSpanElementAtInline(destVar,destType)
        ??this.RenderComputeInline(destVar,destType,index);
    private string? RenderDirectSpanElementAtInline(string destVar,string destType) {
        var span = $"__span{this.result}";
        return this.RenderSpanInline(source,destVar,destType,span,spanName => $$"""
            var __idx={{index}};
            if(__idx<0) throw new global::System.ArgumentOutOfRangeException("index");
            if((uint)__idx>=(uint){{spanName}}.Length) throw new global::System.ArgumentOutOfRangeException("index");
            {{destVar}}={{spanName}}[__idx];
            """);
    }
    private string? RenderRangeElementAtInline(string destVar,string destType) {
        if(source is not RangeRoot range||source.ElementType!="int") {
            return null;
        }
        var start = $"__start{番号}";
        var count = $"__count{番号}";
        var idx = $"__idx{番号}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{start}}={{range.StartExpression}};
                var {{count}}={{range.CountExpression}};
                var {{idx}}={{index}};
                if((uint){{idx}}>=(uint){{count}}) throw new global::System.ArgumentOutOfRangeException(nameof(index));
                {{destVar}}={{start}} + {{idx}};
            }
            """;
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(ref TEnum source, int index){
                if(index<0) throw new global::System.ArgumentOutOfRangeException(nameof(index));
                source.TryGetSpan(out var __span);
                if((uint)index>=(uint)__span.Length) throw new global::System.ArgumentOutOfRangeException(nameof(index));
                return __span[index];
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(ref TEnum source, int index){
                if(index<0) throw new global::System.ArgumentOutOfRangeException(nameof(index));
                var currentIndex=0;
                while(source.TryGetNext(out var __mn24)){
                    if(currentIndex==index) return __mn24;
                    currentIndex++;
                }
                throw new global::System.ArgumentOutOfRangeException(nameof(index));
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

internal class ElementAt_index(int 番号,Enumerable source,string index,string resultType):BufferedScalar(番号,resultType) {
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
        method.Parameters.Length==1
        &&method.Parameters[0].Type is INamedTypeSymbol {
            Name: "Index",
            ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true }
        };
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.EnumeratorTypeName,source.GetEnumeratorExpression,index);
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}, global::System.Index index";
    protected override string ComputeExpression => $"Compute(ref {this.e}, index)";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderRangeElementAtIndexInline(destVar,destType)
        ??this.RenderDirectSpanElementAtIndexInline(destVar,destType)
        ??this.RenderComputeInline(destVar,destType,index);
    private string? RenderDirectSpanElementAtIndexInline(string destVar,string destType) {
        var span = $"__span{this.result}";
        return this.RenderSpanInline(source,destVar,destType,span,spanName => $$"""
            var __idx=({{index}}).GetOffset({{spanName}}.Length);
            if((uint)__idx>=(uint){{spanName}}.Length) throw new global::System.ArgumentOutOfRangeException("index");
            {{destVar}}={{spanName}}[__idx];
            """);
    }
    private string? RenderRangeElementAtIndexInline(string destVar,string destType) {
        if(source is not RangeRoot range||source.ElementType!="int") {
            return null;
        }
        var start = $"__start{番号}";
        var count = $"__count{番号}";
        var idx = $"__idx{番号}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{start}}={{range.StartExpression}};
                var {{count}}={{range.CountExpression}};
                var {{idx}}=({{index}}).GetOffset({{count}});
                if((uint){{idx}}>=(uint){{count}}) throw new global::System.ArgumentOutOfRangeException(nameof(index));
                {{destVar}}={{start}} + {{idx}};
            }
            """;
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(ref TEnum source,global::System.Index index){
                source.TryGetSpan(out var __span);
                var idx=index.GetOffset(__span.Length);
                if((uint)idx>=(uint)__span.Length) throw new global::System.ArgumentOutOfRangeException(nameof(index));
                return __span[idx];
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(ref TEnum source,global::System.Index index){
                if(!index.IsFromEnd){
                    var idx=index.Value;
                    var currentIndex=0;
                    while(source.TryGetNext(out var __mn25)){
                        if(currentIndex==idx) return __mn25;
                        currentIndex++;
                    }
                    throw new global::System.ArgumentOutOfRangeException(nameof(index));
                }else{
                    var n=index.Value;
                    if(n==0) throw new global::System.ArgumentOutOfRangeException(nameof(index));
                    var count=0;
                    var buffer=new {{resultType}}[n];
                    while(source.TryGetNext(out var __mn26)){
                        buffer[count%n]=__mn26;
                        count++;
                    }
                    if(count<n) throw new global::System.ArgumentOutOfRangeException(nameof(index));
                    return buffer[(count-n)%n];
                }
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
