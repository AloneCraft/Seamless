namespace Tsavorite.SourceGenerator.Linq.Fragments;

internal abstract class MinMaxByBase(int 番号,Enumerable source,FuncFragment keySelector,string resultType,string direction):BufferedScalar(番号,resultType){
    protected override Enumerable PrimarySource=>source;
    public override string StructMethod=> this.FuncSupport(keySelector,nameof(keySelector));
    protected override string StructSources=>source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression=>$"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters=>$"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(keySelector)}";
    protected override string ComputeExpression=>$"Compute({this.e}{this.ClosureArgsSuffix(keySelector)})";
    public override string? RenderInlineComputation(string destVar,string destType) {
        var methodName=direction=="greater" ? "MaxBy" : "MinBy";
        return this.RenderComputeInline(destVar,destType)
            ?? (TryRenderDirectLambda(keySelector) is { } lambda
                ? this.RenderDirectEnumerableCallInline(source,destVar,destType,methodName,lambda)
                : null);
    }
    protected override string ComputeMembers=>source.CanTryProvideSpan
        ? this.BuildTrySpanCompute()
        : this.BuildEnumeratorCompute();
    protected override string GetEnumeratorDisposeBody=>$"{this.e}.Dispose();";
    private string CompareOp=>direction=="greater"?">":"<";
    private string BuildSpanCompute()=>$$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(keySelector)}}){
            var comparer=global::System.Collections.Generic.Comparer<{{FuncReturnTypeName(keySelector)}}>.Default;
            source.TryGetSpan(out var __span);
            if(__span.Length==0) throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
            {{resultType}} bestElement=default!;
            {{FuncReturnTypeName(keySelector)}} bestKey=default!;
            var __first=true;
            foreach(var {{this.item}} in __span){
                {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                {{this.FuncAssign(keySelector,nameof(keySelector),this.key,this.item)}}
                if(__first||comparer.Compare({{this.key}},bestKey){{this.CompareOp}}0){
                    __first=false;
                    bestElement={{this.item}};
                    bestKey={{this.key}};
                }
            }
            return bestElement;
        }
        """;
    private string BuildTrySpanCompute()=>$$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(keySelector)}}){
            if(source.TryGetSpan(out var __span)){
                var comparer=global::System.Collections.Generic.Comparer<{{FuncReturnTypeName(keySelector)}}>.Default;
                if(__span.Length==0) throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
                {{resultType}} bestElement=default!;
                {{FuncReturnTypeName(keySelector)}} bestKey=default!;
                var __first=true;
                foreach(var {{this.item}} in __span){
                    {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                    {{this.FuncAssign(keySelector,nameof(keySelector),this.key,this.item)}}
                    if(__first||comparer.Compare({{this.key}},bestKey){{this.CompareOp}}0){
                        __first=false;
                        bestElement={{this.item}};
                        bestKey={{this.key}};
                    }
                }
                return bestElement;
            }
            return ComputeEnumerator(source{{this.ClosureArgsSuffix(keySelector)}});
        }
        {{AggressiveInliningAttribute}}
        private static {{resultType}} ComputeEnumerator(TEnum source{{this.ClosureMethodParameterSuffix(keySelector)}}){
            var comparer=global::System.Collections.Generic.Comparer<{{FuncReturnTypeName(keySelector)}}>.Default;
            if(!source.MoveNext()) throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
            {{resultType}} bestElement=source.Current;
            {{FuncReturnTypeName(keySelector)}} bestKey=default!;
            {{this.FuncAssign(keySelector,nameof(keySelector),"bestKey","bestElement")}}
            while(source.MoveNext()){
                var {{this.item}}=source.Current;
                {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                {{this.FuncAssign(keySelector,nameof(keySelector),this.key,this.item)}}
                if(comparer.Compare({{this.key}},bestKey){{this.CompareOp}}0){
                    bestElement={{this.item}};
                    bestKey={{this.key}};
                }
            }
            return bestElement;
        }
        """;
    private string BuildEnumeratorCompute()=>$$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(keySelector)}}){
            var comparer=global::System.Collections.Generic.Comparer<{{FuncReturnTypeName(keySelector)}}>.Default;
            if(!source.MoveNext()) throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
            {{resultType}} bestElement=source.Current;
            {{FuncReturnTypeName(keySelector)}} bestKey=default!;
            {{this.FuncAssign(keySelector,nameof(keySelector),"bestKey","bestElement")}}
            while(source.MoveNext()){
                var {{this.item}}=source.Current;
                {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                {{this.FuncAssign(keySelector,nameof(keySelector),this.key,this.item)}}
                if(comparer.Compare({{this.key}},bestKey){{this.CompareOp}}0){
                    bestElement={{this.item}};
                    bestKey={{this.key}};
                }
            }
            return bestElement;
        }
        """;
}
