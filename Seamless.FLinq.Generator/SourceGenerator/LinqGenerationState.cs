namespace Seamless.FLinq.Generator;

internal sealed class LinqGenerationConfiguration(
    LinqOptimizations optimizations,
    LinqConvergenceMode convergenceMode,
    bool emitOptimizationTrace) {
    public LinqOptimizations Optimizations { get; } = optimizations;
    public LinqConvergenceMode ConvergenceMode { get; } = convergenceMode;
    public bool EmitOptimizationTrace { get; } = emitOptimizationTrace;
}

internal sealed class GenerationState(FLinqIncrementalGenerator.Linq傍受対象型 target) {
    public FLinqIncrementalGenerator.Linq傍受対象型 Target { get; } = target;
    public LinqGenerationConfiguration Configuration { get; } = target.Configuration;
    public OptimizationTraceCollector? Trace { get; } = target.Configuration.EmitOptimizationTrace
        ? new OptimizationTraceCollector(target.Method.ToDisplayString())
        : null;
    public FLinqIncrementalGenerator.AnonymousStructRecordRegistry? AnonymousRegistry { get; private set; } = new(
        FLinqIncrementalGenerator.AnonRecordFileToken(target.SourcePath)+"_"+target.MethodName);
    public HashSet<string> EmittedUnsafeAccessors { get; } = new(StringComparer.Ordinal);
    public HashSet<string> UsedIdentifiers { get; } = new(StringComparer.Ordinal);
    public List<Fragment> GeneratedFragments { get; } = new();
    public List<string> UnsafeAccessorCodes { get; } = new();

    internal string[] ResolveOuterTypeParameters(INamedTypeSymbol containingType) {
        if(this.Target.MethodTypeParameters.Length>0) {
            return this.Target.MethodTypeParameters;
        }
        var containingTypes = new Stack<INamedTypeSymbol>();
        for(var current = containingType;current is not null;current=current.ContainingType) {
            containingTypes.Push(current);
        }
        return containingTypes
            .SelectMany(static type => type.TypeParameters)
            .Select(static typeParameter => typeParameter.Name)
            .ToArray();
    }

    internal int FragmentNumber;
    internal int LinqInvocationCount;
    internal string StaticMethods = string.Empty;
}

internal sealed class FragmentBuildContext(
    GenerationState state,
    SemanticModel semanticModel,
    INamedTypeSymbol containingType,
    int uniqueNumber) {
    public GenerationState State { get; } = state;
    public SemanticModel SemanticModel { get; } = semanticModel;
    public INamedTypeSymbol ContainingType { get; } = containingType;
    internal int UniqueNumber = uniqueNumber;
    public LinqGenerationConfiguration Configuration => this.State.Configuration;
    public string[] MethodTypeParameters => this.State.Target.MethodTypeParameters;
    internal FragmentRenderContext CreateRenderContext(bool counterInstrumentationEnabled = false) =>
        new(null,this.MethodTypeParameters.ToImmutableArray(),counterInstrumentationEnabled);

    internal void ApplyRenderContext(Fragment fragment,bool counterInstrumentationEnabled = false) {
        fragment.ApplyRenderContext(this.CreateRenderContext(counterInstrumentationEnabled));
    }
}
