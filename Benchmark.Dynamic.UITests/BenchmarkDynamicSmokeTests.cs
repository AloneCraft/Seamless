using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace Benchmark.Dynamic.UITests;

public sealed class BenchmarkDynamicSmokeTests{
    [DynamicUiFact]
    public async Task 起動して主要設定Controlを公開する(){
        var applicationPath=Environment.GetEnvironmentVariable("BENCHMARK_DYNAMIC_APP_PATH");
        Assert.False(
            string.IsNullOrWhiteSpace(applicationPath),
            "BENCHMARK_DYNAMIC_APP_PATHにBenchmark.Dynamic.UI.exeの絶対パスを指定してください。");
        Assert.True(File.Exists(applicationPath),$"Application was not found: {applicationPath}");
        if(Environment.GetEnvironmentVariable("BENCHMARK_DYNAMIC_GUI_MODE")=="IsolatedDesktop"){
            await RunIsolatedAsync(applicationPath!);
            return;
        }
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
        Assert.Empty(driver.FindElements(MobileBy.AccessibilityId("動的設定.実行")));
        Assert.Empty(driver.FindElements(MobileBy.AccessibilityId("動的結果.タブ.System")));
        Assert.Empty(driver.FindElements(MobileBy.AccessibilityId("動的結果.タブ.Seamless")));
        foreach(var id in new[]{
            "動的ライブ.開始","動的ライブ.ランダム探索開始","動的ライブ.停止","動的ライブ.一時停止",
            "動的ライブ.検証対象","動的ライブ.ランキング","動的ライブ.集計",
            "動的ライブ.段階.1","動的ライブ.段階.2","動的ライブ.段階.3",
            "動的詳細.Query再実行","動的詳細.Query縮小"}){
            Assert.NotNull(driver.FindElement(MobileBy.AccessibilityId(id)));
        }
        driver.FindElement(MobileBy.AccessibilityId("動的ライブ.検証対象")).Click();
        foreach(var id in new[]{"動的ライブ.対象.SystemArray","動的ライブ.型.Int","動的ライブ.探索件数","動的ライブ.シード再現"}){
            Assert.NotNull(driver.FindElement(MobileBy.AccessibilityId(id)));
        }
        driver.FindElement(MobileBy.Name("回帰ケース")).Click();
        foreach(var id in new[]{"回帰ケース.検索","回帰ケース.読込","回帰ケース.一覧","回帰ケース.実行","回帰ケース.全実行","回帰ケース.中止","回帰ケース.展開コード","回帰ケース.状態","回帰ケース.詳細"}){
            Assert.NotNull(driver.FindElement(MobileBy.AccessibilityId(id)));
        }
    }

    private static async Task RunIsolatedAsync(string applicationPath){
        var repository=Environment.GetEnvironmentVariable("SEAMLESS_TEST_REPOSITORY")??throw new InvalidOperationException("SEAMLESS_TEST_REPOSITORY is required.");
        var results=Environment.GetEnvironmentVariable("SEAMLESS_TEST_RESULT_ROOT")??throw new InvalidOperationException("SEAMLESS_TEST_RESULT_ROOT is required.");
        var outputDirectory=Path.Combine(results,"gui");
        var start=new System.Diagnostics.ProcessStartInfo("pwsh"){
            UseShellExecute=false,
            CreateNoWindow=true,
            WindowStyle=System.Diagnostics.ProcessWindowStyle.Hidden,
            RedirectStandardOutput=true,
            RedirectStandardError=true,
            WorkingDirectory=repository,
        };
        foreach(var argument in new[]{"-NoProfile","-File",Path.Combine(repository,"tools","testing","Invoke-IsolatedGuiCheck.ps1"),"-Application",applicationPath,"-OutputDirectory",outputDirectory}){
            start.ArgumentList.Add(argument);
        }
        using var process=System.Diagnostics.Process.Start(start)??throw new IOException("Unable to start the isolated GUI check.");
        var output=process.StandardOutput.ReadToEndAsync();
        var error=process.StandardError.ReadToEndAsync();
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(14));
        try{
            await process.WaitForExitAsync(timeout.Token);
            Assert.True(process.ExitCode==0,(await output)+Environment.NewLine+(await error));
        }finally{
            if(!process.HasExited){process.Kill(entireProcessTree:true);await process.WaitForExitAsync();}
        }
        using var result=System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(outputDirectory,"gui-result.json")));
        Assert.Equal("Passed",result.RootElement.GetProperty("Status").GetString());
        Assert.NotEqual(result.RootElement.GetProperty("Desktop").GetString(),result.RootElement.GetProperty("InputDesktop").GetString());
        var checks=result.RootElement.GetProperty("Checks").EnumerateArray().Select(value=>value.GetString()).ToArray();
        Assert.Contains("RepeatedCasePassedWithoutFallback",checks);
        Assert.Contains("SavedExpandedSourceVisible",checks);
    }
}

public sealed class DynamicUiFactAttribute:FactAttribute{
    public DynamicUiFactAttribute(){
        if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BENCHMARK_DYNAMIC_APP_PATH"))){
            this.Skip="BENCHMARK_DYNAMIC_APP_PATHが未設定のためUI自動化を実行しません。";
        }
    }
}
