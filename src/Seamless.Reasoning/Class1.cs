namespace Seamless.Reasoning;
public record BenchmarkConfig(
    long BufferSize,      // バッファサイズ
    int LoopCount,        // ループ回数
    int NumaNode,         // 対象のNUMAノード（沼の指定）
    int ThreadId          // アフィニティ設定用
);

// 計測結果
public record BenchmarkResult(
    string Name,          // ベンチマーク名
    double Value,         // スコア
    string Unit,          // 単位 (GB/s, GFLOPS, ns)
    TimeSpan Elapsed      // 経過時間
);

// ベンチマーク本体のインターフェース
public interface IBenchmark : IDisposable{
    // 1. 準備フェーズ（計測時間に含まない）
    // ここでメモリ確保、データの初期化、スレッドのPin留めを行う
    void Setup(BenchmarkConfig BenchmarkConfig);

    // 2. 計測フェーズ（ここだけを計測する）
    // 戻り値として、計算したバイト数や演算回数を返す
    //void Run();

    // 3. 後始末（IDisposableで代用可だが明示的に定義してもよい）
    void Cleanup();
}

