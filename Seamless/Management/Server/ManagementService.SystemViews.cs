using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Seamless.Management.Server;

public sealed partial class ManagementService{
    private sealed record DatabaseRow(string DatabaseId,string Name,long PublishedVersion,string Status);
    private sealed record DraftRow(string DatabaseId,string Owner,long BaseVersion,long DraftRevision,string Change,DateTimeOffset SavedAt);
    private sealed record DraftFile(DraftRow Row,Draft Draft);
    private sealed record LocationRow(string DatabaseId,long PublishedVersion,string Role,string LogicalPath,string? ResolvedPath,string LinkKind,string Status);
    private sealed record ObjectRow(string Schema,string Name,string Kind,string ReturnType);
    private sealed record PermissionRow(string User,string Scope,string Permissions,bool Disabled);
    private sealed class ViewSet<T>(IEnumerable<T> rows):HashSet<T>(rows),global::Seamless.Collections.ISet<T>;

    private static NativeCatalog.Item SystemView<T>(string name)=>new(new(
        new(name,name,"","SystemView",typeof(T).Name,null),
        RuntimeValueJson.Columns(typeof(T)),[],[],null,new("",0,"",null,0,0,false)));

    private static IEnumerable<NativeCatalog.Item> SystemViews(){
        yield return SystemView<DatabaseRow>("Databases");
        yield return SystemView<DraftRow>("Draft");
        yield return SystemView<LocationRow>("StorageLocation");
    }

    private static IEnumerable<NativeCatalog.Item> DatabaseMetadata(UserAccount actor,DefinitionVersion version){
        var source=new CatalogSource(version.Definition.Id,version.Version,version.ArtifactHash,null,0,0,false);
        foreach(var entry in DatabaseSystemObjects.Visible(Can(actor,"*",ManagementPermission.Manage),Can(actor,version.Database,ManagementPermission.Manage))){
            var shape=entry.Id switch{
                DatabaseSystemObjects.Info=>typeof(DatabaseRow),
                DatabaseSystemObjects.Objects=>typeof(ObjectRow),
                DatabaseSystemObjects.Location=>typeof(LocationRow),
                DatabaseSystemObjects.Permissions=>typeof(PermissionRow),
                _=>throw new InvalidOperationException(entry.Id)
            };
            yield return new(new(entry,RuntimeValueJson.Columns(shape),[],[],
                entry.Kind=="SystemProperty"?shape.Name:null,source));
        }
    }

    private IEnumerable<DefinitionVersion> PlacedDatabases(UserAccount actor)=>this.Records<DefinitionVersion>("active")
        .Where(item=>item.Definition.Kind==DefinitionKind.Database&&!this.store.IsDisplacedPlacement(item)&&Can(actor,item.Database,ManagementPermission.View)&&
            ManagementPlacement.HasDatabaseEntry(this.store.Root,item))
        .OrderBy(item=>item.Database,StringComparer.Ordinal).ThenBy(item=>item.Definition.Id,StringComparer.Ordinal);

    // WALで確定済みの下書きからファイルを再構成する。停止中や手動コピー後も、古いファイルを下書きとして復活させない。
    private HashSet<string> SyncDraftFiles(){
        var directory=Path.Combine(this.store.Root,"drafts");
        SourceWorkspace.CheckLinks(directory);
        Directory.CreateDirectory(directory);
        var names=new HashSet<string>(StringComparer.Ordinal);
        foreach(var draft in this.Records<Draft>("draft").Where(item=>item.Definition.Kind==DefinitionKind.Database&&this.HasDraftChanges(item))){
            names.Add(this.WriteDraftFile(draft));
        }
        return names;
    }

    private bool HasDraftChanges(Draft draft){
        if(draft.DatabaseChange is not null||draft.QueryRenames is {Length:>0}){return true;}
        var active=this.store.Read<DefinitionVersion>("active",draft.Definition.Id);
        if(active is null){return true;}
        var saved=draft.Definition;
        var current=active.Definition;
        var hash=(saved with{Source=saved.Source with{Revision=current.Source.Revision}}).Hash();
        return hash!=current.Hash()&&hash!=(current with{Migration=null}).Hash();
    }

    private string WriteDraftFile(Draft draft){
        var owner=ManagementStore.Normalize(draft.Owner);
        var name=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(owner+"\0"+draft.Definition.Id)))+".json";
        var path=Path.Combine(this.store.Root,"drafts",name);
        SourceWorkspace.CheckLinks(path);
        var change=draft.DatabaseChange is{Delete:true}?"Delete":draft.BaseVersion==0?"Create":draft.DatabaseChange?.NewName is not null?"Rename":"Edit";
        var row=new DraftRow(draft.Definition.Id,draft.Owner,draft.BaseVersion,draft.Revision,change,draft.SavedAt);
        var content=JsonSerializer.Serialize(new DraftFile(row,draft),ManagementJson.Options);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if(!File.Exists(path)||File.ReadAllText(path)!=content){SourceWorkspace.Atomic(path,content);}
        return name;
    }

    private global::Seamless.Collections.ISet<DraftRow> ReadDraftFiles(UserAccount actor){
        var directory=Path.Combine(this.store.Root,"drafts");
        var files=this.SyncDraftFiles();
        return new ViewSet<DraftRow>(Directory.EnumerateFiles(directory,"*.json").Order(StringComparer.Ordinal)
            .Where(path=>files.Contains(Path.GetFileName(path)))
            .Select(path=>JsonSerializer.Deserialize<DraftFile>(File.ReadAllText(path),ManagementJson.Options)
                ??throw new InvalidDataException("下書きの記録がありません。"))
            .Where(file=>ManagementStore.Normalize(file.Row.Owner)==ManagementStore.Normalize(actor.Name)&&
                Can(actor,this.store.CurrentDatabase(file.Draft.Definition),ManagementPermission.Edit))
            .Select(file=>file.Row));
    }

    private TableRows ReadSystemRows(UserAccount user,TableRowsRequest request,CancellationToken cancellation){
        if(request.Limit is <1 or >500||request.Key is not null||request.Cursor is{Offset:<1}){throw new ManagementException("InvalidRequest","システムビューのページ指定が不正です。");}
        lock(this.store.Gate){
            var actor=this.CurrentActor(user);
            if(request.Database==SystemDatabaseId){
                Require(actor,"*",ManagementPermission.Manage);
                return request.Table switch{
                    "Databases"=>Page(new ViewSet<DatabaseRow>(this.PlacedDatabases(actor).Select(version=>new DatabaseRow(
                        version.Definition.Id,version.Database,version.Version,this.runtime.PlacementError(version)??"Available"))
                        .Concat(this.store.UnregisteredPlacements.Select(item=>new DatabaseRow(item.Definition?.Id??"",item.Name,item.Version,item.Error!)))),0),
                    "Draft"=>Page(this.ReadDraftFiles(actor),0),
                    "StorageLocation"=>Page(new ViewSet<LocationRow>(this.PlacedDatabases(actor).SelectMany(this.Locations)),0),
                    _=>throw new ManagementException("NotFound","システムビューがありません。")
                };
            }
            var database=this.store.DatabaseDefinition(request.Database);
            Require(actor,database.Database,ManagementPermission.View);
            switch(DatabaseSystemObjects.CanonicalId(request.Table)){
                case DatabaseSystemObjects.Info:
                    return Page(new ViewSet<DatabaseRow>([new(database.Definition.Id,database.Database,database.Version,
                        Can(actor,"*",ManagementPermission.Manage)?this.runtime.PlacementError(database)??"Available":"Published")]),database.Version);
                case DatabaseSystemObjects.Location:
                    Require(actor,"*",ManagementPermission.Manage);
                    return Page(new ViewSet<LocationRow>(this.Locations(database)),database.Version);
                case DatabaseSystemObjects.Permissions:
                    Require(actor,database.Database,ManagementPermission.Manage);
                    return Page(new ViewSet<PermissionRow>(this.Records<UserAccount>("user").OrderBy(account=>account.Name,StringComparer.Ordinal)
                        .SelectMany(account=>account.Grants.Where(grant=>grant.Database==database.Database||grant.Database=="*")
                            .Select(grant=>new PermissionRow(account.Name,grant.Database,grant.Permissions.ToString(),account.Disabled)))),database.Version);
                case DatabaseSystemObjects.Objects:
                    return this.CatalogRead(actor,new("db:"+database.Definition.Id,""),(_,items)=>Page(
                        new ViewSet<ObjectRow>(items.Select(item=>item.Details).Select(details=>new ObjectRow(
                            details.Object.Schema,details.Object.Name,details.Object.Kind,
                            details.Object.Kind is "View" or "TableFunction" or "SystemView"
                                ? "ISet<"+details.Object.RecordType+">"
                                : details.ReturnType??details.Object.RecordType))),database.Version));
                default:
                    throw new ManagementException("NotFound","システムビューがありません。");
            }
        }

        TableRows Page<T>(global::Seamless.Collections.ISet<T> rows,long version){
            if(request.ExpectedVersion is{} expected&&expected!=version){throw new ManagementException("CatalogChanged","システムビューの公開版が変わりました。");}
            return NativeRuntime.ReadRows(rows,static row=>row,null,typeof(T),request,version,cancellation,
                static row=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(row,ManagementJson.Options)))) with{KeyMembers=[]};
        }
    }

    private IEnumerable<LocationRow> Locations(DefinitionVersion version){
        var logical=Path.Combine(this.store.Root,version.StoragePath);
        var error=this.runtime.PlacementError(version);
        if(error is not null){yield return new(version.Definition.Id,version.Version,"Generation",logical,null,"",error);yield break;}
        var generation=ManagementPlacement.GenerationPath(this.store.Root,version.StoragePath);
        yield return Location("Generation",logical,generation);
        var data=ManagementPlacement.DirectoryPath(Path.Combine(generation,"data"));
        yield return Location("Data",Path.Combine(logical,"data"),data);
        foreach(var role in new[]{"store-v2","wal-v2"}){
            yield return Location(role,Path.Combine(logical,"data",role),ManagementPlacement.DirectoryPath(Path.Combine(data,role)));
        }
        var layout=TableStorage.Read(generation);
        if(layout is not null){
            foreach(var (table,path) in TableStorage.OpenDirectories(generation,layout.Id)!.OrderBy(item=>item.Key,StringComparer.Ordinal)){
                yield return Location("Table:"+table,layout.Locations.Single(item=>item.Table==table).Root,path);
            }
        }
        LocationRow Location(string role,string path,string target)=>new(version.Definition.Id,version.Version,role,path,target,path==target?"Directory":"Link",Directory.Exists(target)?"Available":"Missing");
    }
}
