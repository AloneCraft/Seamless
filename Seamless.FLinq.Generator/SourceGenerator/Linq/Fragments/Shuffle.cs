namespace Seamless.FLinq;

/// <summary>
/// System.Linq.Enumerable.Shuffle() 呼び出しをインターセプトし、
/// Seamless.Linq.Shuffle<TEnumerator, TElement> へ委譲する Fragment。
///
/// 【変更前（独自実装）】
///   List<T> + Random.Shared.Shuffle() によるバッファリング＋シャッフル
///
/// 【変更後（ランタイム委譲）】
///   Seamless.Linq.Shuffle<TEnum, ElementType> をフィールドとして保持し、
///   TryGetNext / TryGetNonEnumeratedCount / TryGetSpan / TryCopyTo / Dispose
///   をすべて委譲する。
///
/// これにより:
/// - ArrayPool (RentedArrayBox) 活用によるメモリ効率向上
/// - ShuffleSkipTake 融合最適化の FLinq 経由での有効化
/// - TryGetSpan / TryCopyTo 最適化の FLinq 経由での利用
/// - 実装の単一化
/// </summary>
internal class Shuffle(int 番号,Enumerable source):Enumerable(番号) {
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
    public override string ElementType => source.ElementType;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum source";
    public override string GetEnumeratorFields => $$"""
        private global::Seamless.Linq.Shuffle<TEnum, {{source.ElementType}}> {{this.e}};
        """;
    public override string GetEnumeratorConstructor => $"this.{this.e} = new global::Seamless.Linq.Shuffle<TEnum, {source.ElementType}>(source);";
    public override string GetTryGetNextBody => $$"""
        if(this.{{this.e}}.TryGetNext(out var {{this.item}})){
            current = this.Current = {{this.item}};
            return true;
        }
        current=default!;
        return false;
        """;

    // ランタイム Shuffle の TryGetNonEnumeratedCount / TryGetSpan / TryCopyTo を委譲
    protected override string GetTryGetNonEnumeratedCountBody =>
        $"return this.{this.e}.TryGetNonEnumeratedCount(out count);";
    protected override string GetTryGetSpanBody =>
        $"return this.{this.e}.TryGetSpan(out span);";
    protected override string GetTryCopyToBody =>
        $"return this.{this.e}.TryCopyTo(destination, __offset);";
    protected override string GetEnumeratorDisposeBody =>
        $"this.{this.e}.Dispose();";
}
