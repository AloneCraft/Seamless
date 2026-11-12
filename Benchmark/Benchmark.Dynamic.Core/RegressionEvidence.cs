namespace Benchmark.Dynamic.Core;

/// <summary>合否条件と分離して保持する、ケース内の測定観測値。</summary>
public static class RegressionEvidence{
    private static readonly AsyncLocal<List<string>?> current=new();
    public static void Record(string observation)=>(current.Value??throw new InvalidOperationException("実行中の回帰ケースがありません。")).Add(observation);
    public static IDisposable Push(List<string> observations){var previous=current.Value;current.Value=observations;return new Scope(()=>current.Value=previous);}
    private sealed class Scope(Action release):IDisposable{public void Dispose()=>release();}
}
