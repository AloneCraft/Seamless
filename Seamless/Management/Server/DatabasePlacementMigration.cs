namespace Seamless.Management.Server;

internal static class DatabasePlacementMigration{
    internal static int Run(string root,string backup,CancellationToken cancellation){
        using var owner=Program.LockOffline(root);
        Program.CopyLocked(root,backup,cancellation);
        using var store=new ManagementStore(root,owner,leaveOwnerOpen:true);
        using var runtime=new NativeRuntime(store,startAuditWorker:false);
        var active=store.PlacementRecords<DefinitionVersion>("active").Where(item=>item.Definition.Kind==DefinitionKind.Database).ToArray();
        var plans=new List<(DefinitionVersion Version,string Entry,string Source,string Destination)>();
        var normalized=new List<DefinitionVersion>();
        foreach(var version in active){
            cancellation.ThrowIfCancellationRequested();
            // ディスクから取り除かれたDBは履歴に残る。入口が残る場合は世代の欠損やリンク切れを報告する。
            if(!ManagementPlacement.HasDatabaseEntry(store.Root,version)){continue;}
            var named=ManagementStore.TryNamedGeneration(version.StoragePath,out var entry,out var folder);
            if(named&&ManagementPlacement.TryVersionDirectory(folder,out _)){
                if(!store.Read<bool>("namedPublication",version.Definition.Id)){
                    var directory=ManagementPlacement.DirectoryPath(Path.Combine(store.Root,"generations",entry),allowShortcut:true);
                    if(Directory.EnumerateFileSystemEntries(directory).Any(path=>
                        ManagementPlacement.TryVersionDirectory(Path.GetFileName(path),out var number)&&number>version.Version)){
                        throw new IOException("旧方式の公開記録より新しい世代があります。公開対象を確認してください。"+directory);
                    }
                    using var compiled=runtime.ReadArtifact(version);
                    ManagementPlacement.CheckData(ManagementPlacement.GenerationPath(store.Root,version.StoragePath));
                    normalized.Add(version);
                }
                continue;
            }
            if(!named){
                if(version.StoragePath.Split(['/', '\\']) is not ["generations",var old]||!Guid.TryParseExact(old,"N",out _)){
                    throw new InvalidDataException("旧公開世代のパスが不正です。"+version.StoragePath);
                }
                entry=version.Database;
            }
            if(!ManagementPlacement.IsEntryName(entry)){throw new InvalidDataException("DB入口の名前が不正です。"+entry);}
            var relative=Path.Combine("generations",entry,ManagementPlacement.VersionDirectory(version.Version));
            var source=ManagementPlacement.GenerationPath(store.Root,version.StoragePath);
            var destination=ManagementPlacement.GenerationPath(store.Root,relative);
            if(Directory.Exists(destination)||File.Exists(destination)){throw new IOException("移行先が既にあります。"+destination);}
            var parent=Path.GetDirectoryName(destination)!;
            if(Directory.Exists(parent)&&Directory.EnumerateFileSystemEntries(parent).Any(path=>
                ManagementPlacement.TryVersionDirectory(Path.GetFileName(path),out var number)&&number>=version.Version)){
                throw new IOException("移行先に公開世代の競合があります。"+parent);
            }
            plans.Add((version,entry,source,relative));
        }
        foreach(var (version,entry,source,destination) in plans){
            cancellation.ThrowIfCancellationRequested();
            using var compiled=runtime.ReadArtifact(version);
            var temporary=Path.Combine("generations",entry,".pending-"+Guid.NewGuid().ToString("N"));
            var staging=ManagementPlacement.GenerationPath(store.Root,temporary);
            NativeRuntime.CopyGenerationDirectory(source,staging,cancellation);
            ManagementPlacement.SealData(staging);
            using(var opened=new DatabaseInstance(version.Definition,Path.Combine(staging,"data"),compiled.Retain())){
                opened.Open();
                using var read=opened.Database.BeginReadSession(cancellation);
                opened.Database.ValidateRelations();
            }
            var prepared=new PreparedDefinition(version.Definition,version.Version-1,version.ArtifactHash,destination){PendingPath=temporary};
            var next=version with{StoragePath=destination};
            cancellation.ThrowIfCancellationRequested();
            store.WritePublication(prepared,()=>{
                store.Put("active",next.Definition.Id,next);
                store.Put("version",next.Definition.Id+":"+next.Version,next);
                foreach(var origin in store.PlacementRecords<AuditOrigin>("auditOrigin").Where(item=>item.StoragePath==version.StoragePath).ToArray()){
                    store.Put("auditOrigin",origin.WalId.ToString("N"),origin with{StoragePath=next.StoragePath});
                }
            });
        }
        if(normalized.Count>0){store.Write(()=>{
            foreach(var version in normalized){store.Put("namedPublication",version.Definition.Id,true);}
        });}
        store.LoadDatabasePlacement();
        foreach(var (version,_,_,_) in plans){
            store.RequirePlacement(store.Read<DefinitionVersion>("active",version.Definition.Id)!);
        }
        return plans.Count+normalized.Count;
    }
}
