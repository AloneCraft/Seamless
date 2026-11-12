using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Worker;
using Benchmark.Infrastructure;
using System.Text.Json;

namespace Benchmark.Dynamic.Tests;

public sealed class RegressionRunStoreTests{
    [Theory]
    [InlineData("Passed",true,"Passed",0)]
    [InlineData("Failed",true,"Failed",0)]
    [InlineData("Skipped",true,"Failed",0)]
    [InlineData("Passed",false,"Failed",0)]
    [InlineData("Passed",true,"Failed",1)]
    public void 保存したケースの状態と解放結果でrunを判定する(string status,bool unloaded,string expected,int fallback){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??Path.GetTempPath(),"regression-store-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);var database=Path.Combine(root,"history.db");var results=Path.Combine(root,"results.jsonl");
        string id;
        using(var store=new RegressionRunStore(database,results,"case")){
            string? error=null;
            if(status!="Passed"){error="reason";}
            id=store.RunID;store.RecordCompilation("generated");store.Record(new("case",status,"observed",error,fallback));store.Complete(unloaded);
        }
        var repository=new BenchmarkRepository(new(database));var run=repository.GetRun(id)!;
        Assert.Equal(expected,run.Status);Assert.NotNull(run.CompletedAt);
        using var settings=JsonDocument.Parse(run.SettingsJSON);Assert.Equal(results,settings.RootElement.GetProperty("ResultsPath").GetString());
        Assert.NotEmpty(settings.RootElement.GetProperty("SourceSHA256").GetString()!);
        Assert.Equal(Fingerprint.Of("generated"),settings.RootElement.GetProperty("GeneratedSHA256").GetString());
        Assert.Equal("generated",File.ReadAllText(settings.RootElement.GetProperty("GeneratedSourcePath").GetString()!));
        var row=JsonSerializer.Deserialize<RegressionCaseResult>(Assert.Single(File.ReadAllLines(results)))!;
        Assert.Equal(status,row.Status);Assert.Empty(repository.GetMeasurements(id));
    }
}
