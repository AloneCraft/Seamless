namespace Test.Benchmark;

public sealed class ベンチマーク実行プロファイル名 {
    [Fact]
    public void フォルダー名に変換() {
        Assert.Equal("Accurate",global::UI.ベンチマーク実行プロファイル名.フォルダー名に変換(global::UI.ベンチマーク実行プロファイル.正確));
        Assert.Equal("Intermediate",global::UI.ベンチマーク実行プロファイル名.フォルダー名に変換(global::UI.ベンチマーク実行プロファイル.中間));
        Assert.Equal("Fast",global::UI.ベンチマーク実行プロファイル名.フォルダー名に変換(global::UI.ベンチマーク実行プロファイル.高速));
        Assert.Equal("Test",global::UI.ベンチマーク実行プロファイル名.フォルダー名に変換(global::UI.ベンチマーク実行プロファイル.テスト));
    }

    [Fact]
    public void 割り当てバイト数取得元を取得() {
        Assert.Equal("BenchmarkDotNet MemoryDiagnoser",global::UI.ベンチマーク実行プロファイル名.割り当てバイト数取得元を取得(global::UI.ベンチマーク実行プロファイル.正確));
        Assert.Equal("BenchmarkDotNet MemoryDiagnoser",global::UI.ベンチマーク実行プロファイル名.割り当てバイト数取得元を取得(global::UI.ベンチマーク実行プロファイル.中間));
        Assert.Equal("GC.GetAllocatedBytesForCurrentThread",global::UI.ベンチマーク実行プロファイル名.割り当てバイト数取得元を取得(global::UI.ベンチマーク実行プロファイル.高速));
        Assert.Equal("Thread.Sleep テスト実行器",global::UI.ベンチマーク実行プロファイル名.割り当てバイト数取得元を取得(global::UI.ベンチマーク実行プロファイル.テスト));
    }
}
