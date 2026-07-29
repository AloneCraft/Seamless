namespace Benchmark.Dynamic.Tests;

[CollectionDefinition(Name,DisableParallelization=true)]
public sealed class DynamicWorkerTestCollection{
    public const string Name="DynamicWorkerSerial";
}
