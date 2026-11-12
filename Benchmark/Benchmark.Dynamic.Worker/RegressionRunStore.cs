using Benchmark.Dynamic.Core;
using Benchmark.Infrastructure;
using System.Text.Json;

namespace Benchmark.Dynamic.Worker;

/// <summary>比較測定を捏造せず、回帰のrun・ソース指紋・ケース別結果を既存履歴とJSONLへ保存する。</summary>
public sealed class RegressionRunStore:IDisposable{
    private readonly BenchmarkRepository repository;
    private readonly StreamWriter results;
    private Run run;
    private bool completed;
    private bool failed;
    private int count;
    public string RunID=>this.run.ID;
    public RegressionRunStore(string database,string resultFile,string? caseID){
        if(!Path.IsPathFullyQualified(resultFile)){throw new ArgumentException("結果ファイルの絶対パスが必要です。",nameof(resultFile));}
        Directory.CreateDirectory(Path.GetDirectoryName(resultFile)!);
        this.results=new(new FileStream(resultFile,FileMode.CreateNew,FileAccess.Write,FileShare.Read),new System.Text.UTF8Encoding(false)){AutoFlush=true};
        try{
            this.repository=new(new(database));
            var settings=JsonSerializer.Serialize(new{CaseID=caseID,Bundle=RegressionCaseSources.Legacy.ID,SourceSHA256=Fingerprint.OfJSON(RegressionCaseSources.Legacy.Documents),ResultsPath=Path.GetFullPath(resultFile)});
            this.run=new(Guid.NewGuid().ToString("N"),"Dynamic.Regression","Running",settings,JsonSerializer.Serialize(EnvironmentSnapshot.Capture("Regression-v1")),BuildFingerprint.Capture(typeof(RegressionRunStore).Assembly.Location),DateTimeOffset.UtcNow);
            this.repository.SaveRun(this.run);
        }catch{this.results.Dispose();throw;}
    }
    public void Record(RegressionCaseResult result){
        if(this.completed){throw new InvalidOperationException("完了したrunへは追記できません。");}
        this.results.WriteLine(JsonSerializer.Serialize(result));this.count++;this.failed|=result.Status!="Passed"||result.FallbackCount!=0;
    }
    public void RecordCompilation(string expandedSource){
        if(this.completed){throw new InvalidOperationException("完了したrunへは追記できません。");}
        var settings=System.Text.Json.Nodes.JsonNode.Parse(this.run.SettingsJSON)!.AsObject();
        var path=settings["ResultsPath"]!.GetValue<string>()+".generated.cs";
        using(var writer=new StreamWriter(new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read),new System.Text.UTF8Encoding(false))){writer.Write(expandedSource);}
        settings["GeneratedSourcePath"]=path;settings["GeneratedSHA256"]=Fingerprint.Of(expandedSource);
        settings["GeneratorFingerprint"]=BuildFingerprint.Capture(typeof(global::Seamless.FLinq.Generator.FLinqIncrementalGenerator).Assembly.Location);
        settings["RuntimeFingerprint"]=BuildFingerprint.Capture(typeof(global::Seamless.Linq.GeneratedCodeHelpers).Assembly.Location);
        this.run=this.run with{SettingsJSON=settings.ToJsonString()};this.repository.SaveRun(this.run);
    }
    public void Complete(bool assemblyUnloaded){
        if(this.completed){return;}
        this.results.Flush();
        var status="Failed";
        if(this.count>0&&!this.failed&&assemblyUnloaded){status="Passed";}
        this.run=this.run with{Status=status,CompletedAt=DateTimeOffset.UtcNow};
        this.repository.SaveRun(this.run);this.completed=true;
    }
    public void Dispose(){
        try{if(!this.completed){this.Complete(false);}}finally{this.results.Dispose();}
    }
}
