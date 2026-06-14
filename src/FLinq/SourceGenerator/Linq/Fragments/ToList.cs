namespace Tsavorite.SourceGenerator.Linq.Fragments;

// 要素が匿名型（ElementType=="TResult"）のチェーンを materialize する ToList。
// 匿名型の型名は生成コードに書けないため、Carrier の汎用 ToList へ型推論で委譲し
// var 宣言の直接実行文として出力する（List の要素型は呼び出し側の推論で閉じる）。
internal sealed class AnonymousExitToList(int 番号,Enumerable source):Scalar(番号),DirectExecutionFragment{
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => source.GetEnumeratorStruct;
    public override string GetTryGetNextBody => string.Empty;
    public override string GetCurrentExpression => string.Empty;
    public string GetDirectExecutionStatement(string actualVariableName,string actualTypeName) =>
        $"var {actualVariableName}=global::Tsavorite.Carrier.ValueEnumerableExtensions.ToList({source.GetEnumerableExpression});";
}

public class ToList(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => $"Compute({this.e})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderDirectEnumerableCallInline(source,destVar,destType,"ToList")
        ?? this.RenderComputeInline(destVar,destType);
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source) {
            global::System.Collections.Generic.List<{{source.ElementType}}> result;
            if(source.TryGetNonEnumeratedCount(out var __count)) {
                result=new(__count);
            }else{
                result=new();
            }
            using var enumerator = source;
            while(enumerator.MoveNext()) result.Add(enumerator.Current);
            return result;
        }
        """;
    protected override string GetEnumeratorDisposeBody => string.Empty;
}
