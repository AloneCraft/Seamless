namespace Seamless.Benchmarks;

// ベンチマークをテストとして簡易計測する際のパラメーター。
// テスト側の既定は1回実行。環境変数で上書きできる。
static class BenchmarkParameters{
    // 各ベンチマークメソッドの計測実行回数。既定1回。
    public static int RunCount{get;}=Read("SEAMLESS_BENCHMARK_RUN_COUNT",1,1);
    // 計測前のウォームアップ回数。既定0回。
    public static int WarmupCount{get;}=Read("SEAMLESS_BENCHMARK_WARMUP_COUNT",0,0);

    static int Read(string name,int fallback,int min){
        var raw=Environment.GetEnvironmentVariable(name);
        if(int.TryParse(raw,out var value)&&value>=min){
            return value;
        }
        return fallback;
    }
}
