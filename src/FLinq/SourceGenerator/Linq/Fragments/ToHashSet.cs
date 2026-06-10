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
