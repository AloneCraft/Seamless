namespace Seamless.SourceGenerator.Linq.Fragments;

public class MaxBy_keySelector(int 番号, Enumerable source, FuncFragment keySelector, string resultType) : BufferedScalar(番号, resultType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(keySelector, nameof(keySelector));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(keySelector)}";
    protected override string ComputeExpression =>
        $"Compute({e}{ClosureArgsSuffix(keySelector)})";
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(keySelector)}}) {
                var comparer=global::System.Collections.Generic.Comparer<{{FuncReturnTypeName(keySelector)}}>.Default;
                source.TryGetSpan(out var __span);
                if(__span.Length==0) throw global::Seamless.GeneratedCodeHelpers.NoElements();
                {{resultType}} maxElement=default!;
                {{FuncReturnTypeName(keySelector)}} maxKey=default!;
                var __first=true;
                foreach(var {{item}} in __span){
                    {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                    {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                    if(__first||comparer.Compare({{key}},maxKey)>0){
                        __first=false;
                        maxElement={{item}};
                        maxKey={{key}};
                    }
                }
                return maxElement;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{ClosureMethodParameterSuffix(keySelector)}}) {
                var comparer=global::System.Collections.Generic.Comparer<{{FuncReturnTypeName(keySelector)}}>.Default;
                if(!source.MoveNext()) throw global::Seamless.GeneratedCodeHelpers.NoElements();
                {{resultType}} maxElement=source.Current;
                {{FuncReturnTypeName(keySelector)}} maxKey=default!;
                {{FuncAssign(keySelector,nameof(keySelector),"maxKey","maxElement")}}
                while(source.MoveNext()){
                    var {{item}}=source.Current;
                    {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                    {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                    if(comparer.Compare({{key}},maxKey)>0){
                        maxElement={{item}};
                        maxKey={{key}};
                    }
                }
                return maxElement;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
