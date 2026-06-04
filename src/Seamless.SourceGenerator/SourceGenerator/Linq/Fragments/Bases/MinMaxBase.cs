namespace Seamless.SourceGenerator.Linq.Fragments;
public abstract class MinMaxBase(int 番号, Enumerable source, string resultType, string direction) : BufferedScalar(番号, resultType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}";
    protected override string ComputeExpression => $"Compute({e})";
    private string CompareCond => direction=="greater" ? "comparer.Compare(value,result)>0" : "comparer.Compare(value,result)<0";
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source) {
                var comparer=global::System.Collections.Generic.Comparer<{{resultType}}>.Default;
                source.TryGetSpan(out var __span);
                if(__span.Length==0) throw global::Seamless.GeneratedCodeHelpers.NoElements();
                {{resultType}} result=default!;
                var __first=true;
                foreach(var value in __span){
                    if(__first||{{CompareCond}}){
                        __first=false;
                        result=value;
                    }
                }
                return result;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source) {
                var comparer=global::System.Collections.Generic.Comparer<{{resultType}}>.Default;
                if(!source.MoveNext()) throw global::Seamless.GeneratedCodeHelpers.NoElements();
                {{resultType}} result=source.Current;
                while(source.MoveNext()){
                    var value=source.Current;
                    if({{CompareCond}}) result=value;
                }
                return result;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
