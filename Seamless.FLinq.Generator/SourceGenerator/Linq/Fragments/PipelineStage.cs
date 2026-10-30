namespace Seamless.FLinq.Generator;

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
    bool innerIsList = false,
    bool innerIsImmutableArray = false,
    bool containsAnchor = false,
    GroupByAggregatePlan? aggregatePlan = null,
    GroupByAggregatePlan? aggregateCandidate = null) {
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
    public bool InnerIsList { get; } = innerIsList;
    public bool InnerIsImmutableArray { get; } = innerIsImmutableArray;
    public string? KeyType { get; } = keyType;
    public bool UsesIndex { get; } = usesIndex;
    public bool Descending { get; } = descending;
    public GroupByAggregatePlan? AggregatePlan { get; } = aggregatePlan;
    // 融合できる集約の候補(段を作るときに解析だけしておく)。融合するかは、段の並びが揃った後で GroupByFusionPass が下流の要求を見て決める。
    // GroupBy の後の Select の段(Select の形)か、結果選択子つきの GroupBy の段(結果選択子の形)に付く。
    public GroupByAggregatePlan? AggregateCandidate { get; } = aggregateCandidate;
    public bool ContainsAnchor { get; } = containsAnchor
        ||func?.ContainsAnchor==true
        ||secondFunc?.ContainsAnchor==true
        ||thirdFunc?.ContainsAnchor==true
        ||(value is not null&&AnchorAnalysis.LooksLikeAnchorText(value));
}
