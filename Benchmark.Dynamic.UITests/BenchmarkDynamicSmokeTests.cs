using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace Benchmark.Dynamic.UITests;

public sealed class BenchmarkDynamicSmokeTests{
    [Fact]
    public void 起動して主要設定Controlを公開する(){
        var applicationPath=Environment.GetEnvironmentVariable("BENCHMARK_DYNAMIC_APP_PATH");
        Assert.False(
            string.IsNullOrWhiteSpace(applicationPath),
            "BENCHMARK_DYNAMIC_APP_PATHにBenchmark.Dynamic.exeの絶対パスを指定してください。");
        Assert.True(File.Exists(applicationPath),$"Application was not found: {applicationPath}");
        var serverText=Environment.GetEnvironmentVariable("BENCHMARK_DYNAMIC_APPIUM_SERVER")
            ??"http://127.0.0.1:4723";
        var options=new AppiumOptions{
            PlatformName="Windows",
            AutomationName="Windows",
        };
        options.AddAdditionalAppiumOption("app",applicationPath);
        using var driver=new WindowsDriver(
            new Uri(serverText),
            options,
            TimeSpan.FromSeconds(30));
        Assert.NotNull(driver.FindElement(MobileBy.AccessibilityId("動的設定.実行")));
        Assert.NotNull(driver.FindElement(MobileBy.AccessibilityId("動的設定.キャンセル")));
        Assert.NotNull(driver.FindElement(MobileBy.AccessibilityId("動的設定.Source.SystemArray")));
        Assert.NotNull(driver.FindElement(MobileBy.AccessibilityId("動的結果.グリッド.SystemArray")));
        Assert.NotNull(driver.FindElement(MobileBy.AccessibilityId("動的詳細.領域")));
        Assert.NotNull(driver.FindElement(MobileBy.AccessibilityId("動的詳細.Query縮小")));
    }
}
