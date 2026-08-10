namespace Seamless.FLinq;

internal enum OperatorExceptionCoverage {
    StandardLinq,
    ExplicitlyExcluded,
}

internal readonly struct OperatorCoverageManifestEntry {
    internal OperatorCoverageManifestEntry(
        string methodName,
        OperatorCoverageKind coverage,
        bool hasRootVariant,
        string argumentShape,
        string resultShape,
        OperatorExceptionCoverage exceptionCoverage) {
        this.MethodName=methodName;
        this.Coverage=coverage;
        this.HasRootVariant=hasRootVariant;
        this.ArgumentShape=argumentShape;
        this.ResultShape=resultShape;
        this.ExceptionCoverage=exceptionCoverage;
    }

    internal string MethodName { get; }
    internal OperatorCoverageKind Coverage { get; }
    internal bool HasRootVariant { get; }
    internal string ArgumentShape { get; }
    internal string ResultShape { get; }
    internal OperatorExceptionCoverage ExceptionCoverage { get; }
}

internal static class OperatorCoverageManifest {
    private static readonly ImmutableHashSet<string> rootMethods = ImmutableHashSet.Create(StringComparer.Ordinal,
        "AsEnumerable","Empty","InfiniteSequence","Range","Repeat","Sequence");
    private static readonly ImmutableHashSet<string> excludedMethods = ImmutableHashSet.Create(StringComparer.Ordinal,
        "Order","OrderDescending","TryGetNonEnumeratedCount");

    internal static ImmutableArray<OperatorCoverageManifestEntry> Create(IEnumerable<string> methodNames) =>
        methodNames.Select(static methodName => {
            var excluded=excludedMethods.Contains(methodName);
            var hasRoot=rootMethods.Contains(methodName);
            var hasPipeline=OperatorPipelineManifest.TryGet(methodName,out _);
            return new OperatorCoverageManifestEntry(
                methodName,
                excluded ? OperatorCoverageKind.Excluded : OperatorCoverageKind.Fragment,
                hasRoot,
                hasPipeline ? "pipeline-overload-family" : "overload-family",
                hasRoot ? "sequence-or-root" : "sequence-or-scalar",
                excluded ? OperatorExceptionCoverage.ExplicitlyExcluded : OperatorExceptionCoverage.StandardLinq);
        }).ToImmutableArray();
}