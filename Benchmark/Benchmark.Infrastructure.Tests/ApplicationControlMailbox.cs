using System.Text.Json;

namespace Benchmark.Infrastructure.Tests;

public sealed class アプリ操作メールボックス{
    [Fact]
    public async Task 送信Async(){
        var ルート=Path.Combine(Path.GetTempPath(),"seamless-app-wire-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(ルート,"requests"));Directory.CreateDirectory(Path.Combine(ルート,"responses"));
        try{
            var 対象=new ExternalTarget(Guid.NewGuid().ToString("N"),123,639285123450000000);
            var 通信=new global::Benchmark.Infrastructure.アプリ操作メールボックス(ルート,対象);
            var 送信=通信.送信Async(new("external-register","Benchmark.Dynamic.UI",対象,["run"]),CancellationToken.None);
            // 送信は最初の応答待ちまで同期的に公開するため、任意の待ち時間は不要。
            var パス=Assert.Single(Directory.GetFiles(Path.Combine(ルート,"requests"),"*.request.json"));
            using(var JSON=JsonDocument.Parse(File.ReadAllBytes(パス))){
                Assert.Equal(対象.Pid,JSON.RootElement.GetProperty("pid").GetInt32());
                Assert.Equal(対象.Start,JSON.RootElement.GetProperty("start").GetInt64());
                var 要求=JSON.RootElement.GetProperty("request");
                Assert.Equal("external-register",要求.GetProperty("operation").GetString());
                Assert.Equal(対象.InstanceId,要求.GetProperty("external").GetProperty("instanceId").GetString());
                Assert.Equal("run",Assert.Single(要求.GetProperty("operations").EnumerateArray()).GetString());
            }
            Assert.Empty(Directory.GetFiles(Path.Combine(ルート,"requests"),"*.tmp"));
            var 名前=Path.GetFileName(パス).Replace(".request.json",".response.json",StringComparison.Ordinal);
            var 一時=Path.Combine(ルート,"responses",名前+".tmp");
            File.WriteAllText(一時,"{\"ok\":true,\"tasks\":[]}");
            File.Move(一時,Path.Combine(ルート,"responses",名前));
            Assert.True((await 送信).Ok);
            Assert.Empty(Directory.GetFiles(Path.Combine(ルート,"responses")));
        }finally{Directory.Delete(ルート,true);}
    }

    [Fact]
    public async Task 終端を保存Async(){
        var ルート=Path.Combine(Path.GetTempPath(),"seamless-app-receipt-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(ルート);
        try{
            var 対象=new ExternalTarget(Guid.NewGuid().ToString("N"),123,639285123450000000);
            var 通信=new global::Benchmark.Infrastructure.アプリ操作メールボックス(ルート,対象);
            var ID=Guid.NewGuid().ToString("N");
            var 操作=new ExternalTaskRecord{Id=ID,ArtifactDirectory=ルート};
            var 終端=new ExternalTerminalReceipt(1,ID,ID,"Benchmark.Dynamic.UI",対象,new string('a',64),1,"Completed","Failed",true,true,true,DateTimeOffset.UtcNow,"run:1");
            await 通信.終端を保存Async(操作,終端);
            using var JSON=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(ルート,"external-terminal.json")));
            Assert.True(JSON.RootElement.GetProperty("operationCompleted").GetBoolean());
            Assert.Equal("Failed",JSON.RootElement.GetProperty("validationOutcome").GetString());
            Assert.Equal(対象.Start,JSON.RootElement.GetProperty("external").GetProperty("start").GetInt64());
            await 通信.終端を保存Async(操作,終端);
            await Assert.ThrowsAsync<InvalidDataException>(()=>通信.終端を保存Async(操作,終端 with{ValidationOutcome="Passed"}));
            Assert.Empty(Directory.GetFiles(ルート,"*.tmp"));
        }finally{Directory.Delete(ルート,true);}
    }
}
