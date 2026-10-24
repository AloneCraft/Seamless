namespace Benchmark.Tpc.Server;
/// <summary>点検 1 件の合否。</summary>
public enum CheckOutcome{
    Pass,
    Fail,
    /// <summary>この実行では判定できない(例: Data-Maintenance がまだ 1 回も動いていない)。合否には数えない。</summary>
    NotExercised,
}
/// <summary>点検 1 件。<see cref="Clause"/> は仕様の条項番号。</summary>
public sealed record CheckResult(string Clause,string Name,CheckOutcome Outcome,string Detail);
