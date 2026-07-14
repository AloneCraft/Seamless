[assembly: CollectionBehavior(DisableTestParallelization=true)]

namespace Benchmark.UI.UITests;

public sealed class ベンチマークUI自動化ファクト属性:FactAttribute {
    public ベンチマークUI自動化ファクト属性() {
        if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BENCHMARK_UI_APP_PATH"))) {
            this.Skip="BENCHMARK_UI_APP_PATH が設定されていません。Benchmark.UIをビルドし、UI自動化を実行する実行ファイルのパスを設定してください。";
        }
    }
}
