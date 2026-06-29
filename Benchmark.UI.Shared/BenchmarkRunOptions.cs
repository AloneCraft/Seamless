namespace Benchmark;

public sealed record BenchmarkRunOption(
    int RunNumber,
    string DisplayName,
    string RunRoot,
    BenchmarkExecutionProfile ExecutionProfile,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int CompletedJobs,
    int TotalJobs,
    IReadOnlyList<int> ElementCounts);
