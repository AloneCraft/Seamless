// ここを編集する場合:
// - システム時計以外のタイミング情報へ差し替える場合
internal sealed class SystemRunVisualScheduler:IRunVisualScheduler {
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public Task DelayAsync(TimeSpan delay,CancellationToken cancellationToken = default) =>
        Task.Delay(delay,cancellationToken);
}
