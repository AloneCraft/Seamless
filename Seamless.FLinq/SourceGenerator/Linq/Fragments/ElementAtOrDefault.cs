namespace Seamless.FLinq.Linq.Fragments;
public class ElementAtOrDefault(int 番号, Enumerable source, string index, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 1 && method.Parameters[0].Type.SpecialType == SpecialType.System_Int32;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {index})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}, int index";
    protected override string ComputeExpression => $"Compute({e}, index)";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        RenderRangeElementAtOrDefaultInline(destVar,destType)
        ?? RenderDirectEnumerableCallInline(source,destVar,destType,"ElementAtOrDefault",index)
        ?? RenderComputeInline(destVar,destType,index);
    private string? RenderRangeElementAtOrDefaultInline(string destVar,string destType) {
        if(source is not RangeRoot range || source.ElementType!="int") {
            return null;
        }
        var start=$"__start{番号}";
        var count=$"__count{番号}";
        var idx=$"__idx{番号}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{start}}={{range.StartExpression}};
                var {{count}}={{range.CountExpression}};
                var {{idx}}={{index}};
                {{destVar}}=(uint){{idx}}<(uint){{count}} ? {{start}} + {{idx}} : default!;
            }
            """;
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source, int index) {
                if(index<0) return default!;
                source.TryGetSpan(out var __span);
                if((uint)index>=(uint)__span.Length) return default!;
                return __span[index];
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source, int index) {
                if(index<0) return default!;
                var currentIndex=0;
                while(source.MoveNext()) {
                    if(currentIndex==index) return source.Current;
                    currentIndex++;
                }
                return default!;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}

public class ElementAtOrDefault_index(int 番号, Enumerable source, string index, string resultType) : BufferedScalar(番号, resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 1
        && method.Parameters[0].Type is INamedTypeSymbol {
            Name: "Index",
            ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true }
        };
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {index})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}, global::System.Index index";
    protected override string ComputeExpression => $"Compute({e}, index)";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        RenderRangeElementAtOrDefaultIndexInline(destVar,destType)
        ?? RenderDirectEnumerableCallInline(source,destVar,destType,"ElementAtOrDefault",index)
        ?? RenderComputeInline(destVar,destType,index);
    private string? RenderRangeElementAtOrDefaultIndexInline(string destVar,string destType) {
        if(source is not RangeRoot range || source.ElementType!="int") {
            return null;
        }
        var start=$"__start{番号}";
        var count=$"__count{番号}";
        var idx=$"__idx{番号}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{start}}={{range.StartExpression}};
                var {{count}}={{range.CountExpression}};
                var {{idx}}=({{index}}).GetOffset({{count}});
                {{destVar}}=(uint){{idx}}<(uint){{count}} ? {{start}} + {{idx}} : default!;
            }
            """;
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source, global::System.Index index) {
                source.TryGetSpan(out var __span);
                var idx=index.GetOffset(__span.Length);
                if((uint)idx>=(uint)__span.Length) return default!;
                return __span[idx];
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source, global::System.Index index) {
                if(!index.IsFromEnd) {
                    var idx=index.Value;
                    var currentIndex=0;
                    while(source.MoveNext()) {
                        if(currentIndex==idx) return source.Current;
                        currentIndex++;
                    }
                    return default!;
                }else{
                    var n=index.Value;
                    if(n==0) return default!;
                    var count=0;
                    var buffer=new {{resultType}}[n];
                    while(source.MoveNext()) {
                        buffer[count%n]=source.Current;
                        count++;
                    }
                    if(count<n) return default!;
                    return buffer[(count-n)%n];
                }
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
