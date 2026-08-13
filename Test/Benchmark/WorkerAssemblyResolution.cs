using System.Reflection;

namespace Test.Benchmark;

public sealed class ワーカーアセンブリ解決 {
    [Fact]
    public void Release版Worker出力に標準タブの全アセンブリが存在する() {
        var Worker出力=Release版Worker出力を取得();
        var アセンブリ名一覧=global::UI.ベンチマークソースタブ一覧.標準全て
            .SelectMany(static タブ => タブ.列一覧.Select(static 列 => 列.アセンブリ名))
            .Where(static アセンブリ名 => !string.IsNullOrWhiteSpace(アセンブリ名))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.NotEmpty(アセンブリ名一覧);
        foreach(var アセンブリ名 in アセンブリ名一覧) {
            var パス=Path.Combine(Worker出力,アセンブリ名+".dll");
            Assert.True(File.Exists(パス),"Release版Worker出力にアセンブリがありません: "+パス);
        }
    }

    [Fact]
    public async Task Release版WorkerでPersistedとTsavoriteのFLinqIEnumerableを実行できる() {
        var Worker出力=Release版Worker出力を取得();
        var Workerパス=Path.Combine(Worker出力,"Worker.dll");
        Assert.True(File.Exists(Workerパス),"Release版Workerがありません: "+Workerパス);
        var 実行ルート=Path.Combine(Path.GetTempPath(),"Seamless-WorkerAssemblyResolution-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(実行ルート);
        var プロセス=global::UI.ベンチマークワーカークライアント.ワーカーを開始(Workerパス,実行ルート,0);
        try {
            Assert.Equal("ready",await プロセス.StandardOutput.ReadLineAsync());
            await using var セッション=new global::UI.ベンチマーク高速ワーカーセッション(プロセス,実行ルート,300_000_000d);
            foreach(var ケース in 代表ケース一覧を取得()) {
                var 結果=await セッション.実行Async(ケース,0,CancellationToken.None).WaitAsync(TimeSpan.FromMinutes(1));
                Assert.True(string.Equals("Completed",結果.状態,StringComparison.Ordinal),"状態="+結果.状態+" ログ="+結果.ログ);
                Assert.True(File.Exists(Path.Combine(結果.成果物フォルダー,"fast-worker.log")));
            }
            await セッション.終了Async(CancellationToken.None);
        } finally {
            global::UI.ベンチマークワーカークライアント.プロセスを終了(プロセス);
            プロセス.Dispose();
            if(Directory.Exists(実行ルート)) {
                Directory.Delete(実行ルート,true);
            }
        }
    }

    private static IEnumerable<global::UI.ベンチマークジョブ> 代表ケース一覧を取得() {
        foreach(var タブ in new[] { global::UI.ベンチマークソースタブ.System永続化,global::UI.ベンチマークソースタブ.SystemTsavorite }) {
            var 定義=global::UI.ベンチマークソースタブ一覧.取得(タブ);
            var 型完全名=タブ==global::UI.ベンチマークソースタブ.System永続化
                ? "Benchmark.Persisted.Linq.Enumerable.IEnumerable.FLinq.IEnumerableFLinqInt32"
                : "Benchmark.Tsavorite.Linq.Enumerable.IEnumerable.FLinq.IEnumerableFLinqInt32";
            yield return new global::UI.ベンチマークジョブ {
                プロジェクト=定義.左プロジェクト,
                アセンブリ名=定義.アセンブリ名を取得(定義.左プロジェクト),
                型完全名=型完全名,
                メソッド名="Count",
                ソースタブ=タブ,
                要素数=4_096,
                ループ回数=1,
                準備済み要素数一覧=[4_096],
            };
        }
    }

    private static string Release版Worker出力を取得() {
        var ディレクトリ=new DirectoryInfo(AppContext.BaseDirectory);
        while(ディレクトリ is not null) {
            if(File.Exists(Path.Combine(ディレクトリ.FullName,"Seamless.slnx"))) {
                return Path.Combine(ディレクトリ.FullName,"Benchmark","Worker","bin","Release","net10.0");
            }
            ディレクトリ=ディレクトリ.Parent;
        }
        throw new DirectoryNotFoundException("Seamless.slnx を基準にRelease版Worker出力を特定できませんでした。");
    }
}
