using System.Runtime.InteropServices;
using System.Text.Json;

namespace Seamless.Management.Server;

internal sealed record GenerationPromotion(string Id,string PendingPath,string FinalPath);
internal sealed record RetiredDatabaseEntry(string Id,string Path);
internal sealed record DatabasePlacement(string Name,string RelativePath,long Version,Definition? Definition,string ArtifactHash,string? Error);

public sealed partial class ManagementStore{
    private readonly Dictionary<string,string> placementErrors=new(StringComparer.Ordinal);
    private DatabasePlacement[] unregisteredPlacements=[];
    private readonly HashSet<string> displacedPlacements=new(StringComparer.Ordinal);
    internal bool IsDisplacedPlacement(DefinitionVersion version)=>this.displacedPlacements.Contains(version.Definition.Id);
    internal IEnumerable<DatabasePlacement> UnregisteredPlacements=>this.unregisteredPlacements;

    internal static bool TryNamedGeneration(string relative,out string name,out string version){
        if(relative.Split(['/', '\\']) is ["generations",var entry,var folder]&&ManagementPlacement.IsEntryName(entry)){
            name=entry;
            version=folder;
            return true;
        }
        name=version="";
        return false;
    }

    internal void RequirePlacement(DefinitionVersion version){
        if(version.Definition.Kind!=DefinitionKind.Database){return;}
        lock(this.gate){
            if(this.placementErrors.TryGetValue(version.Definition.Id,out var error)){throw new ManagementException("RecoveryRequired",error);}
            if(this.Read<GenerationPromotion>("generationPromotion",version.Definition.Id) is not null){
                throw new ManagementException("RecoveryRequired","公開世代の確定が中断しました。管理サーバーを再起動してください。");
            }
        }
    }

    internal void WritePublication(PreparedDefinition prepared,Action write,Action? beforePromotion=null){
        if(prepared.PendingPath is null){this.Write(write);return;}
        lock(this.gate){
            var id=prepared.Definition.Id;
            if(this.Read<GenerationPromotion>("generationPromotion",id) is not null){
                throw new ManagementException("RecoveryRequired","中断した公開の確定には管理サーバーの再起動が必要です。");
            }
            var prior=this.Read<DefinitionVersion>("active",id);
            _=TryNamedGeneration(prepared.StoragePath,out var nextEntry,out _);
            var retired=prior is not null&&TryNamedGeneration(prior.StoragePath,out var previousEntry,out _)&&previousEntry!=nextEntry?previousEntry:null;
            var promotion=new GenerationPromotion(id,prepared.PendingPath,prepared.StoragePath);
            this.Write(()=>{
                write();
                this.Put("generationPromotion",id,promotion);
                this.Put("namedPublication",id,true);
                if(retired is not null){this.RetireDatabasePlacement(prior!);}
            });
            beforePromotion?.Invoke();
            this.CompletePromotion(promotion);
        }
    }

    internal void RetireDatabasePlacement(DefinitionVersion version){
        if(TryNamedGeneration(version.StoragePath,out var entry,out _)){
            this.Put("retiredEntry",entry,new RetiredDatabaseEntry(version.Definition.Id,version.StoragePath));
        }
    }

    private void CompletePromotion(GenerationPromotion promotion){
        if(!TryNamedGeneration(promotion.FinalPath,out var entry,out var folder)||!ManagementPlacement.TryVersionDirectory(folder,out var version)||
            !TryNamedGeneration(promotion.PendingPath,out var temporaryEntry,out var temporary)||entry!=temporaryEntry||
            !temporary.StartsWith(".pending-",StringComparison.Ordinal)||!Guid.TryParseExact(temporary[9..],"N",out _)){
            throw new InvalidDataException("公開世代の確定先が不正です。");
        }
        var source=ManagementPlacement.GenerationPath(this.Root,promotion.PendingPath);
        var destination=ManagementPlacement.GenerationPath(this.Root,promotion.FinalPath);
        var active=this.Read<DefinitionVersion>("active",promotion.Id);
        if(active is null||active.StoragePath!=promotion.FinalPath||active.Version!=version){
            throw new InvalidDataException("確定待ちの公開世代が管理取引と一致しません。");
        }
        var sourceExists=Directory.Exists(source);
        if(sourceExists==Directory.Exists(destination)){throw new IOException("確定待ちの公開世代の配置が不正です。"+source+" / "+destination);}
        var prepared=ReadPlacement(sourceExists?source:destination,active.Database,promotion.FinalPath,version);
        if(prepared.Error is not null||prepared.Definition?.Id!=promotion.Id||prepared.ArtifactHash!=active.ArtifactHash){
            throw new InvalidDataException(prepared.Error??"確定待ちの公開世代の識別情報が一致しません。");
        }
        if(sourceExists){Directory.Move(source,destination);}
        this.Write(()=>this.Put<GenerationPromotion?>("generationPromotion",promotion.Id,null));
    }

    private static DatabasePlacement ReadPlacement(string directory,string name,string relative,long version){
        try{
            SourceWorkspace.CheckLinks(directory);
            var path=Path.Combine(directory,"manifest.json");SourceWorkspace.CheckLinks(path);
            using var input=File.OpenRead(path);
            if(input.Length>64L<<20){throw new InvalidDataException("公開世代の記録が大きすぎます。");}
            var manifest=JsonSerializer.Deserialize<ArtifactManifest>(input,ManagementJson.Options);
            if(manifest is null||manifest.Definition?.Kind!=DefinitionKind.Database||string.IsNullOrWhiteSpace(manifest.Definition.Id)||
                string.IsNullOrWhiteSpace(manifest.Definition.EntryType)||manifest.Definition.Source?.Files is null||
                manifest.PublishedVersion is{} declared&&declared!=version||manifest.Sha256 is not {Length:64}||!manifest.Sha256.All(char.IsAsciiHexDigit)){
                throw new InvalidDataException("公開世代の記録が不正です。");
            }
            var definition=manifest.ReadDefinition(directory);
            string? error=null;
            try{ManagementPlacement.CheckData(directory);}
            catch(Exception failure) when(IsPlacementFailure(failure)){error=failure.Message;}
            return new(name,relative,version,definition,manifest.Sha256,error);
        }catch(Exception failure) when(IsPlacementFailure(failure)){
            return new(name,relative,version,null,"","公開世代を読み取れません。"+directory+" "+failure.Message);
        }
    }

    private static bool IsPlacementFailure(Exception error)=>error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or COMException or ManagementException or ArgumentException;

    internal IEnumerable<T> PlacementRecords<T>(string kind){
        foreach(var subject in this.CatalogSubjects(kind)){
            if(this.Read<T>(kind,subject) is{} item){yield return item;}
        }
    }

    internal void LoadDatabasePlacement(){
        lock(this.gate){
            this.placementErrors.Clear();
            this.displacedPlacements.Clear();
            this.unregisteredPlacements=[];
            foreach(var pending in this.PlacementRecords<GenerationPromotion>("generationPromotion").ToArray()){
                try{this.CompletePromotion(pending);}
                catch(Exception error) when(IsPlacementFailure(error)){this.placementErrors[pending.Id]=error.Message;}
            }
            var records=this.PlacementRecords<DefinitionVersion>("active").ToArray();
            var databases=records.Where(item=>item.Definition.Kind==DefinitionKind.Database).ToDictionary(item=>item.Definition.Id,StringComparer.Ordinal);
            var names=OperatingSystem.IsWindows()?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal;
            var byName=databases.Values.Where(item=>TryNamedGeneration(item.StoragePath,out _,out _)).ToLookup(item=>{
                _=TryNamedGeneration(item.StoragePath,out var entry,out _);
                return entry;
            },names);
            var root=Path.Combine(this.Root,"generations");
            if(!Directory.Exists(root)){return;}
            SourceWorkspace.CheckLinks(root);
            var entries=Directory.EnumerateFileSystemEntries(root).Select(path=>{
                var name=Path.GetFileName(path);
                return OperatingSystem.IsWindows()&&name.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase)?name[..^4]:name;
            }).Where(ManagementPlacement.IsEntryName).Distinct(names);
            var placements=new List<DatabasePlacement>();
            foreach(var entry in entries){
                var logical=Path.Combine(root,entry);
                var relative=Path.Combine("generations",entry);
                try{
                    var directory=ManagementPlacement.DirectoryPath(logical,allowShortcut:true);
                    var latest=Directory.EnumerateFileSystemEntries(directory).Select(Path.GetFileName)
                        .Select(folder=>(Folder:folder!,Version:ManagementPlacement.TryVersionDirectory(folder!,out var number)?number:0))
                        .DefaultIfEmpty().MaxBy(item=>item.Version);
                    if(latest.Version==0){
                        if(byName[entry].Any(item=>TryNamedGeneration(item.StoragePath,out _,out var folder)&&ManagementPlacement.TryVersionDirectory(folder,out _))){
                            placements.Add(new(entry,relative,0,null,"","公開世代がありません。"+logical));
                        }
                        continue;
                    }
                    // 旧サーバーが接尾辞付きで確定した版は、明示移行で正式な世代名に直す。
                    if(byName[entry].Any(item=>item.Version>=latest.Version&&TryNamedGeneration(item.StoragePath,out _,out var folder)&&!ManagementPlacement.TryVersionDirectory(folder,out _))){continue;}
                    var placement=ReadPlacement(Path.Combine(directory,latest.Folder),entry,Path.Combine(relative,latest.Folder),latest.Version);
                    if(placement.Definition is{} definition&&(this.Read<string>("retiredDatabase",definition.Id) is not null||
                        this.Read<RetiredDatabaseEntry>("retiredEntry",entry)==new RetiredDatabaseEntry(definition.Id,placement.RelativePath))){continue;}
                    placements.Add(placement);
                }catch(Exception error) when(IsPlacementFailure(error)){
                    placements.Add(new(entry,relative,0,null,"",error.Message));
                }
            }
            var duplicates=placements.Where(item=>item.Definition is not null).ToLookup(item=>item.Definition!.Id,StringComparer.Ordinal);
            var unmatched=new List<DatabasePlacement>();
            var changes=new List<(DefinitionVersion? Before,DefinitionVersion After)>();
            var selected=new HashSet<string>(StringComparer.Ordinal);
            foreach(var placement in placements){
                var id=placement.Definition?.Id;
                DefinitionVersion? before=null;
                if(id is not null){databases.TryGetValue(id,out before);}
                var error=placement.Error;
                if(before is not null&&placement.Version>before.Version&&TryNamedGeneration(before.StoragePath,out _,out _)&&
                    !this.Read<bool>("namedPublication",before.Definition.Id)){
                    error="旧形式の配置に管理記録より新しい世代があります。"+placement.RelativePath;
                }
                if(id is not null&&duplicates[id].Count()>1){
                    error="同じDB IDの入口が複数あります。"+string.Join(" / ",duplicates[id].Select(item=>item.RelativePath));
                }
                if(error is not null){
                    if(before is not null){this.placementErrors[before.Definition.Id]=error;}
                    foreach(var prior in byName[placement.Name]){this.placementErrors[prior.Definition.Id]=error;}
                    if(!byName[placement.Name].Any()){unmatched.Add(placement with{Error=error});}
                    continue;
                }
                if(id is null||this.placementErrors.ContainsKey(id)){continue;}
                selected.Add(id);
                var stored=this.Read<DefinitionVersion>("version",id+":"+placement.Version);
                var after=new DefinitionVersion(placement.Definition!,placement.Version,stored?.Owner??before?.Owner??"",
                    stored?.CreatedAt??before?.CreatedAt??DateTimeOffset.UtcNow,placement.ArtifactHash,placement.RelativePath){Database=placement.Name};
                if(before is null||before.StoragePath!=after.StoragePath||before.Database!=after.Database||
                    before.Version!=after.Version||before.ArtifactHash!=after.ArtifactHash||before.Definition.Hash()!=after.Definition.Hash()){
                    changes.Add((before,after));
                }
            }
            var namesTaken=placements.Select(item=>item.Name).ToHashSet(names);
            var acceptedNames=placements.Where(item=>item.Definition is{} definition&&selected.Contains(definition.Id))
                .Select(item=>item.Name).ToHashSet(names);
            foreach(var item in databases.Values){
                if(selected.Contains(item.Definition.Id)||!TryNamedGeneration(item.StoragePath,out var entry,out _)||!namesTaken.Contains(entry)){continue;}
                this.placementErrors.TryAdd(item.Definition.Id,"入口を使用する公開DBが変わりました。"+Path.Combine(root,entry));
                if(acceptedNames.Contains(entry)){this.displacedPlacements.Add(item.Definition.Id);}
            }
            this.ApplyPlacementChanges(changes,records,names);
            this.unregisteredPlacements=unmatched.ToArray();
        }
    }

    private void ApplyPlacementChanges(List<(DefinitionVersion? Before,DefinitionVersion After)> changes,DefinitionVersion[] records,StringComparer names){
        if(changes.Count==0){return;}
        var renames=changes.Where(change=>change.Before is{} before&&before.Database!=change.After.Database)
            .ToDictionary(change=>change.Before!.Database,change=>change.After.Database,names);
        var claimed=changes.Select(change=>change.After.Database).ToHashSet(names);
        var historicalPaths=changes.Where(change=>change.Before is{} before&&TryNamedGeneration(before.StoragePath,out var entry,out _)&&
            TryNamedGeneration(change.After.StoragePath,out var next,out _)&&entry!=next)
            .ToDictionary(change=>change.After.Definition.Id,change=>change.After.StoragePath,StringComparer.Ordinal);
        // 既存の名前指定の権限は、以前の公開記録のDB IDから今回の入口へ一括で結び直す。
        // 未関連の名前指定の権限が、新しく発見したDBに適用されることを防ぐ。
        this.Write(()=>{
            foreach(var account in this.PlacementRecords<UserAccount>("user").ToArray()){
                var grants=account.Grants.Where(grant=>grant.Database=="*"||renames.ContainsKey(grant.Database)||!claimed.Contains(grant.Database)||
                    changes.Any(change=>change.Before is{} before&&names.Equals(before.Database,grant.Database)&&names.Equals(before.Database,change.After.Database)))
                    .Select(grant=>renames.TryGetValue(grant.Database,out var renamed)?grant with{Database=renamed}:grant).ToArray();
                if(!grants.SequenceEqual(account.Grants)){this.Put("user",Normalize(account.Name),account with{Grants=grants});}
            }
            foreach(var query in records.Where(item=>item.Definition.Kind==DefinitionKind.Query)){
                if(renames.TryGetValue(query.Database,out var name)){this.Put("active",query.Definition.Id,query with{Database=name});}
            }
            foreach(var draft in this.PlacementRecords<Draft>("draft").ToArray()){
                var change=draft.DatabaseChange;
                if(draft.Definition.Kind==DefinitionKind.Database&&change?.ExpectedName is{} expected&&renames.TryGetValue(expected,out var name)&&
                    changes.Any(item=>item.After.Definition.Id==draft.Definition.Id&&item.Before?.Database==expected)){
                    this.Put("draft",Normalize(draft.Owner)+":"+draft.Definition.Id,draft with{DatabaseChange=change with{ExpectedName=name}});
                }
                else if(draft.Definition.Kind==DefinitionKind.Query&&this.Read<DefinitionVersion>("active",draft.Definition.Id) is null&&
                    renames.TryGetValue(draft.Definition.Database,out var queryDatabase)){
                    this.Put("draft",Normalize(draft.Owner)+":"+draft.Definition.Id,draft with{Definition=draft.Definition with{Database=queryDatabase}});
                }
            }
            foreach(var origin in this.PlacementRecords<AuditOrigin>("auditOrigin").ToArray()){
                var relocated=Relocate(origin.DefinitionId,origin.StoragePath);
                if(relocated!=origin.StoragePath){this.Put("auditOrigin",origin.WalId.ToString("N"),origin with{StoragePath=relocated});}
            }
            foreach(var version in this.PlacementRecords<DefinitionVersion>("version").ToArray()){
                var relocated=Relocate(version.Definition.Id,version.StoragePath);
                if(relocated!=version.StoragePath){this.Put("version",version.Definition.Id+":"+version.Version,version with{StoragePath=relocated});}
            }
            foreach(var (_,after) in changes){
                this.Put("version",after.Definition.Id+":"+after.Version,after);
                this.Put("active",after.Definition.Id,after);
            }
        });
        string Relocate(string id,string relative){
            if(historicalPaths.TryGetValue(id,out var next)&&TryNamedGeneration(next,out var entry,out _)&&
                TryNamedGeneration(relative,out var oldEntry,out var folder)&&
                changes.Any(change=>change.After.Definition.Id==id&&change.Before is{} before&&
                    TryNamedGeneration(before.StoragePath,out var priorEntry,out _)&&oldEntry==priorEntry)){
                return Path.Combine("generations",entry,folder);
            }
            return relative;
        }
    }
}
