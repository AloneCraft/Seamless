namespace UI;

internal static class ベンチマークアプリプロファイル {
#if SCALABLE_BENCHMARK
    public const string タイトル = "Seamless Scalable Benchmark";
    public const string 作業ルート名 = "Scalable";
#else
    public const string タイトル = "Seamless Benchmark";
    public const string 作業ルート名 = "Benchmark";
#endif
}
