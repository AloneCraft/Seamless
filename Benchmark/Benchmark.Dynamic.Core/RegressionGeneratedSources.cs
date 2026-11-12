namespace Benchmark.Dynamic.Core;

/// <summary>現在のケースに対応する生成物。ビルド出力ディレクトリに依存せず形状保証を確認する。</summary>
public static class RegressionGeneratedSources{
    private static readonly AsyncLocal<IReadOnlyList<string>?> current=new();
    public static IReadOnlyList<string> Current=>current.Value??throw new InvalidOperationException("実行中の生成ソースがありません。");
    public static IDisposable Push(IReadOnlyList<string> sources){
        var previous=current.Value;current.Value=sources;return new Scope(()=>current.Value=previous);
    }
    private sealed class Scope(Action release):IDisposable{public void Dispose()=>release();}
}
