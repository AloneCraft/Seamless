namespace Seamless.FLinq.Generator;

/// <summary>Shared scalar implementation for Min and Max without a selector.</summary>
internal abstract class MinMaxBase(int number,Enumerable source,string resultType,string direction):BufferedScalar(number,resultType) {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
    }

    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.EnumeratorTypeName,source.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => $"Compute(ref {this.e})";
    // IEnumerable の根は、ライブラリの Max/Min へ列挙子の構造体を値渡しすると、参照型の共有ジェネリックコードで内側の TryGetNext が
    // インライン化されない。span が取れればライブラリの span 版(MinSpan/MaxSpan)、取れなければ Compute の中で回す
    // (ローカル関数では while(source.TryGetNext(…)){ が列挙子の MoveNext/Current に置き換わる。BufferedScalar.DrainRemainingEnumerator)。
    // 比べ方は System.Linq と同じ: 参照型(null を取り得る型)は null を飛ばし、全部 null か空なら null。値型は空なら例外。
    // null の判定は (object?)x is null と書く(具体の値型でもコンパイルでき、JIT が箱詰めごと消す)。
    private bool UsesOwnLoop => source.ProvidesRemainingEnumerator;
    private string Better => direction=="greater" ? ">0" : "<0";
    protected override string ComputeMembers => this.UsesOwnLoop
        ? $$"""
            private static {{resultType}} Compute(ref TEnum source){
                var comparer=global::System.Collections.Generic.Comparer<{{source.ElementType}}>.Default;
                if(source.TryGetSpan(out var __span)){
                    return global::Seamless.Linq.Enumerable.{{(direction=="greater" ? "MaxSpan" : "MinSpan")}}(__span,comparer);
                }
                {{resultType}} value=default!;
                if((object?)value is null){
                    while(source.TryGetNext(out var __candidate)){
                        if((object?)__candidate is null) continue;
                        value=__candidate;
                        while(source.TryGetNext(out var __current)){
                            if((object?)__current is not null&&comparer.Compare(__current,value){{this.Better}}) value=__current;
                        }
                        return value;
                    }
                    return value;
                }
                if(!source.TryGetNext(out var __head)) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                value=__head;
                while(source.TryGetNext(out var __next)){
                    if(comparer.Compare(__next,value){{this.Better}}) value=__next;
                }
                return value;
            }
            """
        : direction=="greater"
            ? $"private static {resultType} Compute(ref TEnum source) => global::Seamless.Linq.Enumerable.Max(new global::Seamless.Linq.ValueEnumerable<TEnum, {source.ElementType}>(source));"
            : $"private static {resultType} Compute(ref TEnum source) => global::Seamless.Linq.Enumerable.Min(new global::Seamless.Linq.ValueEnumerable<TEnum, {source.ElementType}>(source));";
    // ライブラリへ渡す形は、ライブラリの Max/Min が列挙子の複製を値渡しで受けて using で破棄する。所有者はそこだけにする
    // (BufferedScalar.ComputeOwnsEnumerator)。呼び出し元や終端 enumerator がさらに本体を Dispose すると二重 Return になる。
    // 自前で回す形は参照で受けるだけなので、呼び出し元が破棄する。
    protected override bool ComputeOwnsEnumerator => !this.UsesOwnLoop;
    protected override string GetEnumeratorDisposeBody => this.UsesOwnLoop ? $"{this.e}.Dispose();" : string.Empty;

    public override string? RenderInlineComputation(string destVar,string destType) {
        if(source is RangeRoot range&&source.ElementType=="int") {
            return RangePredicateOptimizer.RenderMinMax(range,number,destVar,destType,direction=="greater");
        }
        var methodName = direction=="greater" ? "MaxSpan" : "MinSpan";
        if(source.DirectArrayExpression is { } arrayExpression) {
            return $$"""
                {{destType}} {{destVar}}=global::Seamless.Linq.Enumerable.{{methodName}}<{{source.ElementType}}>({{arrayExpression}},global::System.Collections.Generic.Comparer<{{source.ElementType}}>.Default);
                """;
        }
        // ArrayRoot 以外(List<T> 等)でも GetDirectSpanExpression を持つ source なら
        // span 経由で MaxSpan/MinSpan(SIMD 実装)に乗せられる。TryGetSpan の実行時プローブを
        // 経由しない DirectSpanRootBase 派生(ListRoot 等)全般に適用される。
        if(this.RenderSpanInline(source,destVar,destType,$"__span{number}",spanName =>
            $"{destVar}=global::Seamless.Linq.Enumerable.{methodName}<{source.ElementType}>({spanName},global::System.Collections.Generic.Comparer<{source.ElementType}>.Default);"
        ) is { } spanResult) {
            return spanResult;
        }
        return this.RenderComputeInline(destVar,destType);
    }
}
