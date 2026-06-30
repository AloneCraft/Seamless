namespace UI;

public sealed partial class BenchmarkRunnerViewModel {
    static partial void ConfigureThresholdOptions(ThresholdOptionSettings settings) {
        settings.MillisecondsScale=1000;
        settings.Unit="s";
    }
}
