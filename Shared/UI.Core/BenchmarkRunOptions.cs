namespace UI;

public sealed record BenchmarkRunOption(
    int RunNumber,
    long UiRunId,
    string DisplayName,
    string RunRoot,
    BenchmarkExecutionProfile ExecutionProfile,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int CompletedJobs,
    int TotalJobs,
    IReadOnlyList<int> ElementCounts);
