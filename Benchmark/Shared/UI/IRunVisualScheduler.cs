namespace UI;

// ここを編集する場合:
// - 遅延注入のテスト用実装追加
internal interface IRunVisualScheduler {
    DateTimeOffset UtcNow { get; }
    Task DelayAsync(TimeSpan delay,CancellationToken cancellationToken = default);
}
