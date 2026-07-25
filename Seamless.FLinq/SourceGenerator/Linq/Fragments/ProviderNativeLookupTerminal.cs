namespace Seamless.FLinq.Linq.Fragments;

/*
ProviderNativeLookupTerminal は provider が O(1) の membership lookup を持つときの Contains 終端。

入力例:
  persistedSet.Contains(value)

生成されるコード例:
  bool __result0=(__source).Contains(value);

設計意図:
  従来は ProviderIterateTerminal が state struct + Invoke + Iterate 呼び出し（25〜30 行）を吐き、
  全要素を走査していた。Phase2-2 で Persisted の Set<T>.Contains は .hindex により O(1) になり、
  ランタイム側（Seamless.Linq/Linq/Contains.cs の provider 特殊化オーバーロード）は既にそれを使っている。
  ここは生成側をランタイムの意味論に合わせるだけで、新しい等価性は導入しない。

適用条件（満たさなければ従来の native scan へ落ちる。fallback で隠さない方針は維持）:
  - 中間段が無い（Where/Select が挟まると比較対象が集合の要素でなくなる）
  - comparer 指定が無い（カスタム comparer は集合自身の等価と一致しない）
*/
public sealed class ProviderNativeLookupTerminal(int 番号,string lookupExpression,string resultType)
    :Scalar(番号), DirectExecutionFragment {
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => string.Empty;
    public override string GetTryGetNextBody => string.Empty;

    public string GetDirectExecutionStatement(string actualVariableName,string actualTypeName) =>
        $"{actualTypeName} {actualVariableName}=({actualTypeName})({lookupExpression});";

    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.GetDirectExecutionStatement(destVar,destType);

    public override string InlineSupportingMembers => string.Empty;

    private string ResultTypeName => resultType;
}
