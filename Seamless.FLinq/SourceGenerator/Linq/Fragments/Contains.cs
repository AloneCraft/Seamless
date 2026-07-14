namespace Seamless.FLinq.Linq.Fragments;

public class Contains(int 番号,Enumerable source,string value,string resultType):BufferedScalar(番号,resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length==1;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {value})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}, {source.ElementType} value";
    protected override string ComputeExpression => $"Compute({this.e}, value)";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderRangeContainsInline(destVar,destType)
        ??this.RenderDirectSpanContainsInline(destVar,destType)
        ??this.RenderComputeInline(destVar,destType,value);
    private string? RenderDirectSpanContainsInline(string destVar,string destType) {
        var span = $"__span{this.result}";
        return this.RenderSpanInline(source,destVar,destType,span,spanName => $$"""
            var comparer=global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
            var __value={{value}};
            {{destVar}}=false;
            foreach(var item in {{spanName}}){
                if(comparer.Equals(item,__value)) {
                    {{destVar}}=true;
                    break;
                }
            }
            """);
    }
    private string? RenderRangeContainsInline(string destVar,string destType) {
        if(source is not RangeRoot range||source.ElementType!="int") {
            return null;
        }
        var start = $"__start{番号}";
        var count = $"__count{番号}";
        var target = $"__value{番号}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{start}}={{range.StartExpression}};
                var {{count}}={{range.CountExpression}};
                var {{target}}={{value}};
                {{destVar}}=(uint)({{target}} - {{start}}) < (uint){{count}};
            }
            """;
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source, {{source.ElementType}} value) {
                var comparer=global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
                source.TryGetSpan(out var __span);
                foreach(var item in __span){
                    if(comparer.Equals(item,value)) return true;
                }
                return false;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source, {{source.ElementType}} value) {
                var comparer=global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
                while(source.MoveNext()){
                    if(comparer.Equals(source.Current,value)) return true;
                }
                return false;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

public class Contains_comparer(int 番号,Enumerable source,string value,string comparer,string resultType):BufferedScalar(番号,resultType) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length==2;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {value}, {comparer})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}, {source.ElementType} value, global::System.Collections.Generic.IEqualityComparer<{source.ElementType}>? comparer";
    protected override string ComputeExpression => $"Compute({this.e}, value, comparer)";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderDirectSpanContainsComparerInline(destVar,destType)
        ??this.RenderComputeInline(destVar,destType,value,comparer);
    private string? RenderDirectSpanContainsComparerInline(string destVar,string destType) {
        var span = $"__span{this.result}";
        return this.RenderSpanInline(source,destVar,destType,span,spanName => $$"""
            var __comparer={{comparer}};
            __comparer??=global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
            var __value={{value}};
            {{destVar}}=false;
            foreach(var item in {{spanName}}){
                if(__comparer.Equals(item,__value)) {
                    {{destVar}}=true;
                    break;
                }
            }
            """);
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source, {{source.ElementType}} value, global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer) {
                comparer??=global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
                source.TryGetSpan(out var __span);
                foreach(var item in __span){
                    if(comparer.Equals(item,value)) return true;
                }
                return false;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source, {{source.ElementType}} value, global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer) {
                comparer??=global::System.Collections.Generic.EqualityComparer<{{source.ElementType}}>.Default;
                while(source.MoveNext()){
                    if(comparer.Equals(source.Current,value)) return true;
                }
                return false;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
