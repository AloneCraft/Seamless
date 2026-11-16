namespace Seamless.Management;

internal sealed record SourceBuildInspectionContext(string ProjectId,DefinitionExport Current,SourceProjectBinding Binding);

public sealed partial class SourceProjectService{
    internal async Task<SourceProjectBuild> InspectBuildAsync(SourceProjectBuildRequest request,
        Func<string,CancellationToken,Task<Draft>> getDraft,Func<SourceBuildInspectionContext,string,CancellationToken,SourceProjectBuild> inspect,CancellationToken cancellationToken){
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,this.stopping.Token);
        await this.gate.WaitAsync(cancellation.Token).ConfigureAwait(false);
        try{
            if(!this.projects.TryGetValue(request.Id,out var project)){throw new ManagementException("NotFound","ソースプロジェクトを開き直してください。");}
            var workspace=project.Workspace;
            if(!workspace.IsDatabaseProject){throw new ManagementException("Unsupported","作成元DBの同期記録が必要です。");}
            await this.CheckProjectAsync(project,cancellation.Token).ConfigureAwait(false);
            var current=workspace.Synchronize(workspace.Current.Definition);
            if(current.Definition.Source.Hash()!=request.ExpectedSourceHash||workspace.NeedsDraftSave){
                throw new ManagementException("Conflict","ソースを確認し、DB下書きへ反映してから成果物を検査してください。");
            }
            var saved=await getDraft(current.Definition.Id,cancellation.Token).ConfigureAwait(false);
            if(saved.Revision!=current.DraftRevision||saved.BaseVersion!=current.BaseVersion||saved.Definition.Hash()!=current.Definition.Hash()){
                throw new ManagementException("Conflict","DB下書きが変更されています。ソースと下書きを確認してください。");
            }
            var context=new SourceBuildInspectionContext(request.Id,current with{Definition=current.Definition.Freeze()},workspace.State.ProjectBinding!);
            // DLL の場所が空なら、Manager がこのプロジェクトを SDK でビルドし、その出力を検査する(IDE のビルドと同じ出力先・同じ DLL)。
            var assemblyPath=request.AssemblyPath;
            if(string.IsNullOrWhiteSpace(assemblyPath)){assemblyPath=await BuildWithSdkAsync(project.Path,project.TargetFramework,project.Configuration,cancellation.Token).ConfigureAwait(false);}
            return await Task.Run(()=>inspect(context,assemblyPath,cancellation.Token),cancellation.Token).ConfigureAwait(false);
        }finally{this.gate.Release();}
    }

    // dotnet build でプロジェクトをビルドし、出力の DLL の場所(TargetPath)を返す。失敗したらビルドの出力の終わりを付けて止める。
    private static async Task<string> BuildWithSdkAsync(string path,string target,string configuration,CancellationToken cancellationToken){
        ValidateConfiguration(configuration);
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);deadline.CancelAfter(TimeSpan.FromMinutes(10));
        using var process=new System.Diagnostics.Process{StartInfo=new(DotnetHost()){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(path)!,RedirectStandardOutput=true,RedirectStandardError=true}};
        foreach(var argument in new[]{"build",path,"-nologo","-v:minimal","-m:1","-nodeReuse:false","-p:UseSharedCompilation=false","-p:Configuration="+configuration}){process.StartInfo.ArgumentList.Add(argument);}
        if(target.Length>0){
            if(target.Length>128||target.Any(character=>!char.IsAsciiLetterOrDigit(character)&&character is not '.' and not '-')){throw new ManagementException("InvalidRequest","ターゲット名が不正です。");}
            process.StartInfo.ArgumentList.Add("-p:TargetFramework="+target);
        }
        if(!process.Start()){throw new ManagementException("Unavailable","SDKを起動できません。");}
        try{
            var output=ReadOutputAsync(process.StandardOutput,deadline);var error=ReadOutputAsync(process.StandardError,deadline);
            await Task.WhenAll(output,error,process.WaitForExitAsync(deadline.Token)).ConfigureAwait(false);
            if(process.ExitCode!=0){
                var log=(await output.ConfigureAwait(false))+(await error.ConfigureAwait(false));if(log.Length>8000){log="…"+log[^8000..];}
                throw new ManagementException("BuildFailed","SDKのビルドに失敗しました。\n"+log);
            }
        }finally{
            if(!process.HasExited){process.Kill(entireProcessTree:true);await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);}
        }
        var evaluation=await EvaluateAsync(path,target,cancellationToken,configuration).ConfigureAwait(false);
        var assembly=evaluation.GetProperty("Properties").GetProperty("TargetPath").GetString();
        if(string.IsNullOrWhiteSpace(assembly)||!File.Exists(assembly)){throw new ManagementException("BuildFailed","SDKのビルドの出力が見つかりません: "+assembly);}
        return assembly;
    }
}
