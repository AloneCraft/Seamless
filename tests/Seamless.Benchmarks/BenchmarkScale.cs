namespace Seamless.Collections.Benchmarks;

/// <summary>
/// ベンチマーク全体のスケール定数。
/// 各ベンチマークメソッドは <c>const int Count</c> を個別に持ち、そこに本スケールを掛けて使用する。
/// <see cref="Scale"/> を書き換えると全ベンチマークの要素数が同時にスケールする。
/// </summary>
/// <remarks>
/// <c>Count</c> の基準値は「Parallel=1, MutableFraction=1.0, Unmanaged の設定で
/// 1 メソッドの計測部分が概ね 1 秒で終わる要素数」とする。
/// 計測は <c>tools/BenchmarkCalibration</c> コンソールで倍々探索する。
/// </remarks>
public static class BenchmarkScale {
    /// <summary>各メソッドの const Count にかかる倍率(整数倍率)。</summary>
    public const int Scale = 1;
}
