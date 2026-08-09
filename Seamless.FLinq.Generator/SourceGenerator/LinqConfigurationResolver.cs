namespace Seamless.FLinq;

internal static class LinqConfigurationResolver {
    public static LinqGenerationConfiguration Resolve(IMethodSymbol method,Compilation compilation) {
        return new LinqGenerationConfiguration(
            FLinqIncrementalGenerator.LinqOptimizations判定(method,compilation),
            FLinqIncrementalGenerator.LinqConvergenceMode判定(method,compilation),
            FLinqIncrementalGenerator.EmitOptimizationTrace判定(method,compilation));
    }
}

internal static class LinqTargetDiscoveryService {
    public static FLinqIncrementalGenerator.Linq傍受探索結果? Discover(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken) {
        return FLinqIncrementalGenerator.Linq傍受対象生成(context,cancellationToken);
    }
}