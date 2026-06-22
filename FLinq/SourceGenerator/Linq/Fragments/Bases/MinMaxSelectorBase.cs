namespace Seamless.FLinq.Linq.Fragments;
public abstract class MinMaxSelectorBase(int 番号, Enumerable source, FuncFragment selector, string resultType, string direction) : BufferedScalar(番号, resultType) {
    private string SelectorFuncType => $"global::System.Func<{source.ElementType}, {FuncReturnTypeName(selector)}>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(selector,nameof(selector));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(selector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(selector)}";
    protected override string ComputeExpression =>
        $"Compute({this.e}{this.ClosureArgsSuffix(selector)})";
    public override string? RenderInlineComputation(string destVar,string destType) {
        var methodName = direction=="greater" ? "Max" : "Min";
        return this.RenderComputeInline(destVar,destType)
            ?? (TryRenderDirectLambda(selector) is { } lambda
                ? this.RenderDirectEnumerableCallInline(source,destVar,destType,methodName,lambda)
                : null);
    }
    private string CompareCond => direction=="greater" ? "comparer.Compare(value,result)>0" : "comparer.Compare(value,result)<0";
    protected override string ComputeMembers => source.CanTryProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(selector)}}) {
                var comparer=global::System.Collections.Generic.Comparer<{{resultType}}>.Default;
                {{resultType}} result=default!;
                if(source.TryGetSpan(out var __span)){
                    if(__span.Length==0) throw global::Seamless.Collections.Tsavorite.GeneratedCodeHelpers.NoElements();
                    var __first=true;
                    foreach(var {{this.item}} in __span){
                        {{resultType}} value=default!;
                        {{this.FuncAssignIndented("            ",selector,nameof(selector),"value",this.item)}}
                        if(__first||{{this.CompareCond}}){
                            __first=false;
                            result=value;
                        }
                    }
                }else{
                    {{source.ElementType}} {{this.item}};
                    if(!source.MoveNext()) throw global::Seamless.Collections.Tsavorite.GeneratedCodeHelpers.NoElements();
                    {{this.item}}=source.Current;
                    {{this.FuncAssignIndented("        ",selector,nameof(selector),"result",this.item)}}
                    while(source.MoveNext()){
                        {{this.item}}=source.Current;
                        {{resultType}} value=default!;
                        {{this.FuncAssignIndented("            ",selector,nameof(selector),"value",this.item)}}
                        if({{this.CompareCond}}) result=value;
                    }
                }
                return result;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(selector)}}) {
                var comparer=global::System.Collections.Generic.Comparer<{{resultType}}>.Default;
                {{source.ElementType}} {{this.item}};
                {{resultType}} result=default!;
                if(!source.MoveNext()) throw global::Seamless.Collections.Tsavorite.GeneratedCodeHelpers.NoElements();
                {{this.item}}=source.Current;
                {{this.FuncAssignIndented("    ",selector,nameof(selector),"result",this.item)}}
                while(source.MoveNext()){
                    {{this.item}}=source.Current;
                    {{resultType}} value=default!;
                    {{this.FuncAssignIndented("        ",selector,nameof(selector),"value",this.item)}}
                    if({{this.CompareCond}}) result=value;
                }
                return result;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
