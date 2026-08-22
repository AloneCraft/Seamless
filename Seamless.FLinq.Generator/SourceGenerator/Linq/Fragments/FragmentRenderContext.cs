namespace Seamless.FLinq.Generator;

/// <summary>
/// Fragmentの生成時に共有する描画情報です。生成対象ごとに値を固定し、Fragment間で可変な状態を共有しません。
/// </summary>
internal sealed class FragmentRenderContext {
    internal static FragmentRenderContext Empty { get; } = new(null,ImmutableArray<string>.Empty,false);

    internal static FragmentRenderContext ForOuterTypeParameters(IEnumerable<string> outerTypeParameters) =>
        new(null,outerTypeParameters.ToImmutableArray(),false);

    internal FragmentRenderContext(string? containingTypeName,ImmutableArray<string> outerTypeParameters,bool counterInstrumentationEnabled) {
        this.ContainingTypeName=containingTypeName;
        this.OuterTypeParameters=outerTypeParameters.IsDefault ? ImmutableArray<string>.Empty : outerTypeParameters;
        this.CounterInstrumentationEnabled=counterInstrumentationEnabled;
    }

    internal string? ContainingTypeName { get; }
    internal ImmutableArray<string> OuterTypeParameters { get; }
    internal bool CounterInstrumentationEnabled { get; }

    internal FragmentRenderContext WithContainingTypeName(string? name,ImmutableArray<string> outerTypeParameters) =>
        new(name,outerTypeParameters,this.CounterInstrumentationEnabled);

    internal FragmentRenderContext WithCounterInstrumentation(bool enabled) =>
        new(this.ContainingTypeName,this.OuterTypeParameters,enabled);
}
