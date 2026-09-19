using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using Verification.Core;
namespace Verification.Infrastructure;

public sealed class DotnetVerificationAdapter:IVerificationAdapter{
    public string Name=>"dotnet-test";
    public async Task PrepareAsync(VerificationCase item,string repositoryRoot,string artifactDirectory,CancellationToken cancellationToken){
        if(item.PrepareArguments.Length==0){return;}
        var directory=Path.Combine(artifactDirectory,"build-"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(item.Id)))[..16]);
        Directory.CreateDirectory(directory);
        var result=await ExecuteProcessAsync(item.PrepareArguments,repositoryRoot,directory,cancellationToken).ConfigureAwait(false);
        if(result.ExitCode!=0){throw new InvalidOperationException("ビルド失敗: "+item.Project+" ログ: "+directory);}
        var projectDirectory=Path.GetDirectoryName(Path.Combine(repositoryRoot,item.Project))!;
        var binaryRoot=Path.Combine(projectDirectory,"bin","Release");
        if(Directory.Exists(binaryRoot)){
            await using var output=File.Create(Path.Combine(directory,"binaries.sha256"));
            await using var writer=new StreamWriter(output);
            foreach(var file in Directory.EnumerateFiles(binaryRoot,"*.dll",SearchOption.AllDirectories).Order(StringComparer.Ordinal)){
                cancellationToken.ThrowIfCancellationRequested();
                using var source=File.OpenRead(file);
                await writer.WriteLineAsync(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(source))+" "+Path.GetRelativePath(repositoryRoot,file)).ConfigureAwait(false);
            }
        }
    }
    public async Task<CaseResult> ExecuteAsync(VerificationCase item,string repositoryRoot,string artifactDirectory,CancellationToken cancellationToken){
        var result=await ExecuteProcessAsync(item.Arguments,repositoryRoot,artifactDirectory,cancellationToken).ConfigureAwait(false);
        if(result.ExitCode!=0){return new(item.Id,CaseStatus.Failed,item.Conditions,[],null,null,"テストプロセス終了コード: "+result.ExitCode,result.Peak);}
        var files=Directory.GetFiles(artifactDirectory,"*.trx",SearchOption.AllDirectories);
        if(files.Length==0){return new(item.Id,CaseStatus.Failed,item.Conditions,[],null,null,"TRXがありません。テストの成功を確認できません。",result.Peak);}
        long executed=0,failed=0,skipped=0,total=0;
        foreach(var file in files){
            foreach(var counter in XDocument.Load(file).Descendants().Where(static x=>x.Name.LocalName=="Counters")){
                executed+=Read(counter,"executed");failed+=Read(counter,"failed")+Read(counter,"error")+Read(counter,"timeout")+Read(counter,"aborted");skipped+=Read(counter,"notExecuted");total+=Read(counter,"total");
            }
        }
        var status=CaseStatus.Passed;
        if(failed>0||executed==0){status=CaseStatus.Failed;}
        else if(skipped>0||executed<total){status=CaseStatus.Interrupted;}
        return new(item.Id,status,item.Conditions,[],null,"tests:"+executed+":"+failed+":"+skipped,"実行="+executed+" 失敗="+failed+" 未実行="+skipped,result.Peak);
    }
    private static long Read(XElement element,string attribute){
        if(long.TryParse(element.Attribute(attribute)?.Value,out var value)){return value;}
        return 0;
    }
    private static async Task<(int ExitCode,long Peak)> ExecuteProcessAsync(string[] arguments,string root,string artifacts,CancellationToken cancellationToken){
        using var process=new Process{StartInfo=new("dotnet"){WorkingDirectory=root,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true}};
        foreach(var argument in arguments){process.StartInfo.ArgumentList.Add(argument.Replace("{artifacts}",artifacts,StringComparison.Ordinal));}
        await File.WriteAllTextAsync(Path.Combine(artifacts,"command.json"),JsonSerializer.Serialize(process.StartInfo.ArgumentList),cancellationToken).ConfigureAwait(false);
        process.Start();
        await using var output=File.Create(Path.Combine(artifacts,"stdout.log"));
        await using var error=File.Create(Path.Combine(artifacts,"stderr.log"));
        var stdout=process.StandardOutput.BaseStream.CopyToAsync(output,CancellationToken.None);
        var stderr=process.StandardError.BaseStream.CopyToAsync(error,CancellationToken.None);
        long peak=0;
        try{
            while(!process.HasExited){
                cancellationToken.ThrowIfCancellationRequested();
                process.Refresh();
                peak=Math.Max(peak,process.PrivateMemorySize64);
                await Task.Delay(50,cancellationToken).ConfigureAwait(false);
            }
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }catch{
            if(!process.HasExited){process.Kill(entireProcessTree:true);}
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }finally{
            await Task.WhenAll(stdout,stderr).ConfigureAwait(false);
        }
        return(process.ExitCode,peak);
    }
}
