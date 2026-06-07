namespace Seamless.SourceGenerator.Linq.Fragments;

internal abstract class MinMaxByBase(int 番号,Enumerable source,FuncFragment keySelector,string resultType,string direction):BufferedScalar(番号,resultType){
    protected override Enumerable PrimarySource=>source;
    public override string StructMethod=> this.FuncSupport(keySelector,nameof(keySelector));
    protected override string StructSources=>source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression=>$"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters=>$"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(keySelector)}";
    protected override string ComputeExpression=>$"Compute({this.e}{this.ClosureArgsSuffix(keySelector)})";
    protected override string ComputeMembers=>source.CanProvideSpan
        ? this.BuildSpanCompute()
        : this.BuildEnumeratorCompute();
    protected override string GetEnumeratorDisposeBody=>$"{this.e}.Dispose();";
    private string CompareOp=>direction=="greater"?">":"<";
    private string BuildSpanCompute()=>$$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(keySelector)}}){
            var comparer=global::System.Collections.Generic.Comparer<{{FuncReturnTypeName(keySelector)}}>.Default;
            source.TryGetSpan(out var __span);
            if(__span.Length==0) throw global::Seamless.GeneratedCodeHelpers.NoElements();
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
    private string BuildEnumeratorCompute()=>$$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(keySelector)}}){
            var comparer=global::System.Collections.Generic.Comparer<{{FuncReturnTypeName(keySelector)}}>.Default;
            if(!source.MoveNext()) throw global::Seamless.GeneratedCodeHelpers.NoElements();
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
