namespace Benchmark.UI.UITests;

public sealed class BenchmarkUiAutomationFactAttribute:FactAttribute{
    public BenchmarkUiAutomationFactAttribute(){
        if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BENCHMARK_UI_APP_PATH"))){
            this.Skip="BENCHMARK_UI_APP_PATH is not set. Build Benchmark.UI and set the executable path to run UI automation.";
        }
    }
}
