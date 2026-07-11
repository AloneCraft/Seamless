using UI;

namespace UI.ViewModels;

public sealed partial class RunnerViewModel {
    static partial void ConfigureThresholdOptions(ThresholdOptionSettings settings) {
        settings.MillisecondsScale=1000;
        settings.Unit="s";
    }
}
