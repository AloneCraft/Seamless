using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Seamless.Management.Server;

internal static class EmptyDatabaseRebuild{
    internal static string Verify(string root,string id,long version,string artifact){
        using var owner=Program.LockOffline(root);
        using var store=new ManagementStore(root,owner,true);
        var active=Target(store,id,version,artifact);
        using var runtime=new NativeRuntime(store,startAuditWorker:false);
        var generation=Path.GetFullPath(Path.Combine(store.Root,active.StoragePath));
        var relative=Path.GetRelativePath(store.Root,generation);
        if(!relative.StartsWith("generations"+Path.DirectorySeparatorChar,StringComparison.Ordinal)){
            throw new InvalidDataException("公開世代が管理領域の世代フォルダーにありません。");
        }
        SourceWorkspace.CheckLinks(Path.Combine(generation,"data"));
        using(var instance=new DatabaseInstance(active.Definition,Path.Combine(generation,"data"),runtime.ReadArtifact(active))){
            instance.Open();
            RequireEmpty(instance);
        }
        return Receipt(store.InstanceId,id,version,artifact);
    }

    internal static OperationInfo Run(string root,string id,long version,string artifact,string backup,string verificationCopy,string verifier,CancellationToken cancellation){
        root=Path.GetFullPath(root);backup=Path.GetFullPath(backup);verificationCopy=Path.GetFullPath(verificationCopy);verifier=Path.GetFullPath(verifier);
        foreach(var path in new[]{root,backup,verificationCopy,verifier}){SourceWorkspace.CheckLinks(path);}
        if(Path.GetRelativePath(backup,verificationCopy).Split(Path.DirectorySeparatorChar)[0]!=".."||
            Path.GetRelativePath(verificationCopy,backup).Split(Path.DirectorySeparatorChar)[0]!=".."){
            throw new ArgumentException("バックアップと検証用コピーは同じ階層の独立した領域を指定してください。");
        }
        if(!File.Exists(verifier)){throw new FileNotFoundException("旧ホストの検証用実行ファイルがありません。",verifier);}
        using var owner=Program.LockOffline(root);
        // 管理領域を開く前に退避する。旧ホストで開くのは独立した検証用コピーだけ。
        Program.CopyLocked(root,backup,cancellation);
        Program.CopyLocked(root,verificationCopy,cancellation);
        using var store=new ManagementStore(root,owner,true);
        var active=Target(store,id,version,artifact);
        RequireVerified(verifier,verificationCopy,store.InstanceId,id,version,artifact,cancellation);
        var nextVersion=checked(version+1);
        var drafts=Records<Draft>(store,"draft").Where(draft=>draft.Definition.Id==id&&draft.BaseVersion==version)
            .Select(draft=>draft with{BaseVersion=nextVersion,Revision=checked(draft.Revision+1)}).ToArray();
        var operation=new OperationInfo(Guid.NewGuid(),"offline-maintenance",active.Database,id,OperationState.Running,null,null,DateTimeOffset.UtcNow,BackupPath:backup);
        var builder=new DraftSdkBuilder(Path.Combine(store.Root,"sdk-drafts"),store.SourceBuilds);
        using var compiled=builder.Build(active.Definition,cancellation);
        using var runtime=new NativeRuntime(store,startAuditWorker:false);
        runtime.Publish(active.Definition,cancellation,publication=>{
            cancellation.ThrowIfCancellationRequested();
            _=Target(store,id,version,artifact);
            if(publication.Dependents.Length>0){throw new ManagementException("Conflict","依存する登録クエリがあります。");}
            var candidate=publication.Primary;
            var next=new DefinitionVersion(active.Definition,nextVersion,operation.Owner,DateTimeOffset.UtcNow,candidate.ArtifactHash,candidate.StoragePath){Database=active.Database};
            var complete=operation with{State=OperationState.Succeeded,PublishedVersion=nextVersion};
            store.Write(()=>{
                if(candidate.AuditOrigin is{} origin){store.Put("auditOrigin",origin.WalId.ToString("N"),origin);}
                store.Put("version",id+":"+nextVersion,next);store.Put("active",id,next);
                foreach(var draft in drafts){store.Put("draft",ManagementStore.Normalize(draft.Owner)+":"+id,draft);}
                store.Put("operation",operation.Id.ToString("N"),complete);
                store.AddHistory(operation.Owner,active.Database,"EmptyDatabaseRebuilt",id,nextVersion);
            });
            operation=complete;
        },selected:compiled,fill:(instance,_)=>RequireEmpty(instance));
        return operation;
    }

    private static DefinitionVersion Target(ManagementStore store,string id,long version,string artifact){
        var active=store.Read<DefinitionVersion>("active",id);
        if(active is null||active.Version!=version||active.ArtifactHash!=artifact){
            throw new ManagementException("Conflict","公開版または成果物が指定と一致しません。");
        }
        if(active.Definition.Hash()!=DatabaseDesigner.Empty(id,active.Definition.Database).Hash()){
            throw new ManagementException("Validation","製品の空DBテンプレートに一致する公開版だけを復旧できます。");
        }
        if(Records<DefinitionVersion>(store,"active").Any(item=>item.Definition.Kind==DefinitionKind.Query&&
            string.Equals(store.CurrentDatabase(item.Definition),active.Database,StringComparison.OrdinalIgnoreCase))){
            throw new ManagementException("Validation","公開済みの登録クエリがあるため復旧できません。");
        }
        if(Records<OperationInfo>(store,"operation").Any(operation=>(operation.DefinitionId==id||operation.Database==active.Database)&&
            operation.State is OperationState.Running or OperationState.OutcomeUnknown)){
            throw new ManagementException("Conflict","結果未確定の操作があるため復旧できません。");
        }
        return active;
    }

    private static void RequireEmpty(DatabaseInstance instance){
        if(DatabaseInstance.StorageCount(instance.Database.GetType())!=0){
            throw new ManagementException("Validation","保存領域を持つ利用者の宣言があるため復旧できません。");
        }
    }

    private static IEnumerable<T> Records<T>(ManagementStore store,string kind){
        long cursor=0;
        do{
            var page=store.List<T>(kind,cursor);
            foreach(var item in page.Items){yield return item;}
            if(!page.HasMore){yield break;}
            cursor=page.NextCursor;
        }while(true);
    }

    private static string Receipt(Guid instance,string id,long version,string artifact)=>
        $"{instance:N} {id} {version.ToString(CultureInfo.InvariantCulture)} {artifact}";

    private static void RequireVerified(string verifier,string root,Guid instance,string id,long version,string artifact,CancellationToken cancellation){
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromMinutes(3));
        using var process=new Process{StartInfo=new(verifier){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,
            StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8,WorkingDirectory=Path.GetDirectoryName(verifier)!}};
        foreach(var argument in new[]{"verify-empty-database","--root",root,"--database-id",id,"--version",version.ToString(CultureInfo.InvariantCulture),"--artifact",artifact}){
            process.StartInfo.ArgumentList.Add(argument);
        }
        cancellation.ThrowIfCancellationRequested();
        if(!process.Start()){throw new IOException("旧ホストの検証プロセスを起動できません。");}
        try{
            var output=process.StandardOutput.ReadToEndAsync(deadline.Token);
            var error=process.StandardError.ReadToEndAsync(deadline.Token);
            process.WaitForExitAsync(deadline.Token).GetAwaiter().GetResult();
            if(process.ExitCode!=0||output.GetAwaiter().GetResult().Trim()!=Receipt(instance,id,version,artifact)){
                throw new ManagementException("RecoveryRequired","旧ホストで公開版を検証できません。"+error.GetAwaiter().GetResult().Trim());
            }
        }finally{if(!process.HasExited){process.Kill(entireProcessTree:true);process.WaitForExit();}}
    }
}
