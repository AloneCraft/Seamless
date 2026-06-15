namespace Tsavorite.SourceGenerator.Linq.Fragments;

public abstract class ToHashSetBase(int 番号,Enumerable source,string resultType):BufferedScalar(番号,resultType){
    protected override Enumerable PrimarySource=>source;
    public override string StructMethod=>string.Empty;
    protected override string StructSources=>source.GetEnumeratorStruct;
    protected override string GetEnumeratorDisposeBody=>string.Empty;
}

public class ToHashSet(int 番号,Enumerable source,string resultType):ToHashSetBase(番号,source,resultType){
    public static bool IsApplicableOverload(IMethodSymbol method)=>method.Parameters.Length==0;
    public override string GetEnumeratorExpression=>$"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters=>$"TEnum {this.e}";
    protected override string ComputeExpression=>$"Compute({this.e})";
    public override string? RenderInlineComputation(string destVar,string destType)=>
        this.RenderRangeToHashSetInline(destVar,destType)
        ?? this.RenderDirectEnumerableCallInline(source,destVar,destType,"ToHashSet")
        ?? this.RenderComputeInline(destVar,destType);
    private string? RenderRangeToHashSetInline(string destVar,string destType) {
        if(source is not RangeRoot range || source.ElementType!="int") {
            return null;
        }

        var start=$"__start{番号}";
        var count=$"__count{番号}";
        var index=$"__i{番号}";
        var value=$"__value{番号}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{start}}={{range.StartExpression}};
                var {{count}}={{range.CountExpression}};
                {{destVar}}=new({{count}});
                for(var {{index}}=0;{{index}}<{{count}};{{index}}++) {
                    var {{value}}={{start}} + {{index}};
                    {{destVar}}.Add({{value}});
                }
            }
            """;
    }
    protected override string ComputeMembers=>$$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source){
            global::System.Collections.Generic.HashSet<{{source.ElementType}}> result;
            if(source.TryGetNonEnumeratedCount(out var __count)){
                result=new(__count);
            }else{
                result=new();
            }
            while(source.MoveNext()) result.Add(source.Current);
            return result;
        }
        """;
}

public class ToHashSet_comparer(int 番号,Enumerable source,string comparer,string resultType):ToHashSetBase(番号,source,resultType){
    public static bool IsApplicableOverload(IMethodSymbol method)=>
        method.Parameters.Length==1&&method.Parameters[0].Name=="comparer";
    public override string GetEnumeratorExpression=>$"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression},{comparer})";
    public override string GetEnumeratorPrimaryConstructorParameters=>
        $"TEnum {this.e},global::System.Collections.Generic.IEqualityComparer<{source.ElementType}>? comparer";
    protected override string ComputeExpression=>$"Compute({this.e},comparer)";
    public override string? RenderInlineComputation(string destVar,string destType)=>
        this.RenderRangeToHashSetInline(destVar,destType)
        ?? this.RenderDirectEnumerableCallInline(source,destVar,destType,"ToHashSet",comparer)
        ?? this.RenderComputeInline(destVar,destType,comparer);
    private string? RenderRangeToHashSetInline(string destVar,string destType) {
        if(source is not RangeRoot range || source.ElementType!="int") {
            return null;
        }

        var start=$"__start{番号}";
        var count=$"__count{番号}";
        var comparerVar=$"__comparer{番号}";
        var index=$"__i{番号}";
        var value=$"__value{番号}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{start}}={{range.StartExpression}};
                var {{count}}={{range.CountExpression}};
                var {{comparerVar}}={{comparer}};
                {{destVar}}=new({{count}},{{comparerVar}});
                for(var {{index}}=0;{{index}}<{{count}};{{index}}++) {
                    var {{value}}={{start}} + {{index}};
                    {{destVar}}.Add({{value}});
                }
            }
            """;
    }
    protected override string ComputeMembers=>$$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source,global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer){
            global::System.Collections.Generic.HashSet<{{source.ElementType}}> result;
            if(source.TryGetNonEnumeratedCount(out var __count)){
                result=new(__count,comparer);
            }else{
                result=new(comparer);
            }
            while(source.MoveNext()) result.Add(source.Current);
            return result;
        }
        """;
}
