namespace Seamless.SourceGenerator.Linq.Fragments;
public abstract class MinMaxSelectorBase(int 番号, Enumerable source, FuncFragment selector, string resultType, string direction) : BufferedScalar(番号, resultType) {
    private string SelectorFuncType => $"global::System.Func<{source.ElementType}, {FuncReturnTypeName(selector)}>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(selector,nameof(selector));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(selector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(selector)}";
    protected override string ComputeExpression =>
        $"Compute({e}{ClosureArgsSuffix(selector)})";
    private string CompareCond => direction=="greater" ? "comparer.Compare(value,result)>0" : "comparer.Compare(value,result)<0";
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(selector)}}) {
                var comparer=global::System.Collections.Generic.Comparer<{{resultType}}>.Default;
                {{resultType}} result=default!;
                source.TryGetSpan(out var __span);
                if(__span.Length==0) throw global::Seamless.GeneratedCodeHelpers.NoElements();
                var __first=true;
                foreach(var {{item}} in __span){
                    {{resultType}} value=default!;
                    {{FuncAssign(selector,nameof(selector),"value",item)}}
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
            private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(selector)}}) {
                var comparer=global::System.Collections.Generic.Comparer<{{resultType}}>.Default;
                {{source.ElementType}} {{item}};
                {{resultType}} result=default!;
                if(!source.MoveNext()) throw global::Seamless.GeneratedCodeHelpers.NoElements();
                {{item}}=source.Current;
                {{FuncAssign(selector,nameof(selector),"result",item)}}
                while(source.MoveNext()){
                    {{item}}=source.Current;
                    {{resultType}} value=default!;
                    {{FuncAssign(selector,nameof(selector),"value",item)}}
                    if({{CompareCond}}) result=value;
                }
                return result;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
