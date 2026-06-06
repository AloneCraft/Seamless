namespace Seamless.SourceGenerator.Linq.Fragments;

internal abstract class MinMaxByBase(int 番号,Enumerable source,FuncFragment keySelector,string resultType,string direction):BufferedScalar(番号,resultType){
    protected override Enumerable PrimarySource=>source;
    public override string StructMethod=>FuncSupport(keySelector,nameof(keySelector));
    protected override string StructSources=>source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression=>$"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters=>$"TEnum {e}{ClosurePrimaryCtorSuffix(keySelector)}";
    protected override string ComputeExpression=>$"Compute({e}{ClosureArgsSuffix(keySelector)})";
    protected override string ComputeMembers=>source.CanProvideSpan
        ? BuildSpanCompute()
        : BuildEnumeratorCompute();
    protected override string GetEnumeratorDisposeBody=>$"{e}.Dispose();";
    private string CompareOp=>direction=="greater"?">":"<";
    private string BuildSpanCompute()=>$$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(keySelector)}}){
            var comparer=global::System.Collections.Generic.Comparer<{{FuncReturnTypeName(keySelector)}}>.Default;
            source.TryGetSpan(out var __span);
            if(__span.Length==0) throw global::Seamless.GeneratedCodeHelpers.NoElements();
            {{resultType}} bestElement=default!;
            {{FuncReturnTypeName(keySelector)}} bestKey=default!;
            var __first=true;
            foreach(var {{item}} in __span){
                {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                if(__first||comparer.Compare({{key}},bestKey){{CompareOp}}0){
                    __first=false;
                    bestElement={{item}};
                    bestKey={{key}};
                }
            }
            return bestElement;
        }
        """;
    private string BuildEnumeratorCompute()=>$$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(keySelector)}}){
            var comparer=global::System.Collections.Generic.Comparer<{{FuncReturnTypeName(keySelector)}}>.Default;
            if(!source.MoveNext()) throw global::Seamless.GeneratedCodeHelpers.NoElements();
            {{resultType}} bestElement=source.Current;
            {{FuncReturnTypeName(keySelector)}} bestKey=default!;
            {{FuncAssign(keySelector,nameof(keySelector),"bestKey","bestElement")}}
            while(source.MoveNext()){
                var {{item}}=source.Current;
                {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                if(comparer.Compare({{key}},bestKey){{CompareOp}}0){
                    bestElement={{item}};
                    bestKey={{key}};
                }
            }
            return bestElement;
        }
        """;
}
