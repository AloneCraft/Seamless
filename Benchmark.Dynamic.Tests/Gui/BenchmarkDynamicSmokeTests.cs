using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using Benchmark.Dynamic.Infrastructure;
using Benchmark.Dynamic.Core;
using System.Text.Json;

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
            "動的ライブ.開始","動的ライブ.停止","動的ライブ.一時停止",
            "動的ライブ.Source","動的ライブ.要素型","動的ライブ.Seed","動的ライブ.試行番号",
            "動的ライブ.シード再現","動的ライブ.テストコピー",
            "動的詳細.Query再実行","動的詳細.Query縮小"}){
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
        await VerifySavedRunsAsync(outputDirectory);
    }

    private static async Task VerifySavedRunsAsync(string outputDirectory){
        var path=Path.Combine(outputDirectory,"profile","Benchmark.db");
        Assert.True(File.Exists(path));
        var database=new DynamicDatabase(path);
        await using var writer=new DynamicSingleWriterQueue(database);
        var repository=new DynamicRepository(database,writer);
        var runs=repository.GetRuns().OrderBy(static run=>run.CreatedAt).ToArray();
        Assert.Equal(2,runs.Length);
        using var displayed=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(outputDirectory,"live-results.json")));
        Assert.Equal(2,displayed.RootElement.GetArrayLength());
        var hashes=new List<string>();
        var values=new List<string>();
        var inputs=new List<string>();
        var evidence=new List<object>();
        for(var index=0;index<runs.Length;index++){
            var snapshot=repository.GetRunSnapshot(runs[index].RunID)!;
            var query=Assert.Single(snapshot.Queries,static query=>query.Ordinal==2);
            var execution=Assert.Single(snapshot.SourceExecutions,item=>item.QueryID==query.QueryID);
            Assert.Equal("Succeeded",execution.Status);
            Assert.True(execution.AssemblyUnloaded);
            Assert.DoesNotContain("FLinqRuntimeFallback",execution.Error);
            Assert.Equal(new[]{"FLinq","FLinqUnoptimized","System","ZLinq"},snapshot.Measurements
                .Where(item=>item.ExecutionID==execution.ExecutionID)
                .Select(static item=>item.ImplementationKind).Order(StringComparer.Ordinal));
            var validation=Assert.Single(snapshot.Diagnostics,item=>item.ExecutionID==execution.ExecutionID&&item.Name=="ValidationResults");
            using var artifact=JsonDocument.Parse(validation.DetailsJSON);
            Assert.Equal(4,artifact.RootElement.GetProperty("results").EnumerateObject().Count());
            Assert.Empty(artifact.RootElement.GetProperty("exceptions").EnumerateObject());
            var comparisons=artifact.RootElement.GetProperty("comparisons").EnumerateObject().ToArray();
            Assert.Equal(3,comparisons.Length);
            Assert.All(comparisons,static item=>Assert.True(item.Value.GetProperty("isMatch").GetBoolean()));
            var shown=displayed.RootElement[index];
            Assert.Equal(query.SourceText.Replace("\r",string.Empty).TrimEnd(),shown.GetProperty("Source").GetString()!.Replace("\r",string.Empty).TrimEnd());
            Assert.Contains("namespace Intercept",shown.GetProperty("Expanded").GetString());
            hashes.Add(QueryWorkloadIdentity.Compute(QueryIrJson.Deserialize(query.CanonicalIRJSON)));
            var conditions=Assert.Single(snapshot.Diagnostics,item=>item.ExecutionID==execution.ExecutionID&&item.Name=="MeasurementConditions");
            inputs.Add(JsonSerializer.Deserialize<DynamicMeasurementConditions>(conditions.DetailsJSON)!.InputHash);
            values.Add(artifact.RootElement.GetProperty("results").GetRawText());
            evidence.Add(new{runs[index].RunID,query.QueryID,query.QueryHash,execution.Status,execution.AssemblyUnloaded,Comparisons=comparisons.Length});
        }
        Assert.Equal(hashes[0],hashes[1]);
        Assert.False(string.IsNullOrWhiteSpace(inputs[0]));
        Assert.Equal(inputs[0],inputs[1]);
        Assert.Equal(values[0],values[1]);
        await File.WriteAllTextAsync(Path.Combine(outputDirectory,"persistence-proof.json"),JsonSerializer.Serialize(evidence,new JsonSerializerOptions{WriteIndented=true}));
    }
}

public sealed class DynamicUiFactAttribute:FactAttribute{
    public DynamicUiFactAttribute(){
        if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BENCHMARK_DYNAMIC_APP_PATH"))){
            this.Skip="BENCHMARK_DYNAMIC_APP_PATHが未設定のためUI自動化を実行しません。";
        }
    }
}
