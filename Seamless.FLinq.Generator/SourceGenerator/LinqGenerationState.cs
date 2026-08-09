namespace Seamless.FLinq;

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
    private int AnonymousRegistryPinCount { get; set; }
    public HashSet<string> EmittedUnsafeAccessors { get; } = new(StringComparer.Ordinal);
    public HashSet<string> UsedIdentifiers { get; } = new(StringComparer.Ordinal);
    public List<Fragment> GeneratedFragments { get; } = new();
    public List<string> UnsafeAccessorCodes { get; } = new();

    public void PinAnonymousRegistry() {
        this.AnonymousRegistryPinCount++;
    }

    public void UnpinAnonymousRegistry() {
        this.AnonymousRegistryPinCount--;
    }

    public FLinqIncrementalGenerator.AnonymousStructRecordRegistry? SuspendAnonymousRegistry() {
        var saved=this.AnonymousRegistry;
        if(this.AnonymousRegistryPinCount==0) {
            this.AnonymousRegistry=null;
        }
        return saved;
    }

    public void RestoreAnonymousRegistry(FLinqIncrementalGenerator.AnonymousStructRecordRegistry? saved) {
        this.AnonymousRegistry=saved;
    }
}

internal sealed class FragmentBuildContext(
    GenerationState state,
    SemanticModel semanticModel,
    INamedTypeSymbol containingType,
    int uniqueNumber) {
    public GenerationState State { get; } = state;
    public SemanticModel SemanticModel { get; } = semanticModel;
    public INamedTypeSymbol ContainingType { get; } = containingType;
    public int UniqueNumber { get; set; } = uniqueNumber;
    public LinqGenerationConfiguration Configuration => this.State.Configuration;
    public string[] MethodTypeParameters => this.State.Target.MethodTypeParameters;
}