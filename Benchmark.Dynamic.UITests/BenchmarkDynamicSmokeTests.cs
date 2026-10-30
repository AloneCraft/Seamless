using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace Benchmark.Dynamic.UITests;

public sealed class BenchmarkDynamicSmokeTests{
    [DynamicUiFact]
    public void 起動して主要設定Controlを公開する(){
        var applicationPath=Environment.GetEnvironmentVariable("BENCHMARK_DYNAMIC_APP_PATH");
        Assert.False(
            string.IsNullOrWhiteSpace(applicationPath),
            "BENCHMARK_DYNAMIC_APP_PATHにBenchmark.Dynamic.UI.exeの絶対パスを指定してください。");
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

        // 要素型の選択欄(int 以外の要素型を選べる)。
        Assert.NotNull(driver.FindElement(MobileBy.AccessibilityId("動的設定.要素型")));

        // ライブ検証タブ。SfTabItem は AutomationId ではなく Name で探す。
        driver.FindElement(MobileBy.Name("ライブ検証")).Click();
        foreach(var id in new[]{
            "動的ライブ.開始","動的ライブ.停止","動的ライブ.一時停止","動的ライブ.シード再現",
            "動的ライブ.Source","動的ライブ.要素型","動的ライブ.速度","動的ライブ.Seed","動的ライブ.試行番号",
            "動的ライブ.段階.1","動的ライブ.段階.2","動的ライブ.段階.3"}){
            Assert.NotNull(driver.FindElement(MobileBy.AccessibilityId(id)));
        }
    }
}

public sealed class DynamicUiFactAttribute:FactAttribute{
    public DynamicUiFactAttribute(){
        if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BENCHMARK_DYNAMIC_APP_PATH"))){
            this.Skip="BENCHMARK_DYNAMIC_APP_PATHが未設定のためUI自動化を実行しません。";
        }
    }
}
