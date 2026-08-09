namespace Seamless.FLinq;

internal sealed class PipelineStage(
    PipelineStageKind kind,
    FuncFragment? func,
    string? value,
    string inputType,
    string outputType,
    FuncFragment? secondFunc = null,
    FuncFragment? thirdFunc = null,
    Enumerable? secondSource = null,
    string? innerElementType = null,
    string? keyType = null,
    bool usesIndex = false,
    bool descending = false,
    bool innerIsArray = false,
    bool containsAnchor = false) {
    public PipelineStageKind Kind { get; } = kind;
    public FuncFragment? Func { get; } = func;
    public FuncFragment? SecondFunc { get; } = secondFunc;
    public FuncFragment? ThirdFunc { get; } = thirdFunc;
    public string? Value { get; } = value;
    public string InputType { get; } = inputType;
    public string OutputType { get; } = outputType;
    public Enumerable? SecondSource { get; } = secondSource;
    public string? InnerElementType { get; } = innerElementType;
    // SelectMany の collection selector が配列 (T[]) を返す場合、IEnumerator<T> を介さず
    // インデックスベースで直接走査する specialize パスを使う。
    public bool InnerIsArray { get; } = innerIsArray;
    public string? KeyType { get; } = keyType;
    public bool UsesIndex { get; } = usesIndex;
    public bool Descending { get; } = descending;
    public bool ContainsAnchor { get; } = containsAnchor
        ||func?.ContainsAnchor==true
        ||secondFunc?.ContainsAnchor==true
        ||thirdFunc?.ContainsAnchor==true
        ||(value is not null&&AnchorAnalysis.LooksLikeAnchorText(value));
}
