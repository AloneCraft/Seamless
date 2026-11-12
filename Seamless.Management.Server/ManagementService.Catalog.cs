namespace Seamless.Management.Server;

public sealed partial class ManagementService{
    public const string SystemDatabaseId="system";
    public CatalogPage<CatalogDatabase> Databases(UserAccount user,long cursor=0,string versionToken=""){
        lock(this.store.Gate){var token=this.CatalogToken(versionToken);if(cursor<0){throw new ManagementException("InvalidRequest","ページ指定が不正です。");}
            var page=this.store.SelectPage<DefinitionVersion,CatalogDatabase>("active",cursor,100,item=>item.Definition.Kind==DefinitionKind.Database&&Can(user,item.Database,ManagementPermission.View),item=>new("db:"+item.Definition.Id,item.Database,item.Definition.Id,item.Version,item.ArtifactHash,false));
            var items=page.Items;if(cursor==0&&Can(user,"*",ManagementPermission.Manage)){items=[new(SystemDatabaseId,"system","",0,"",true),..items];}
            return new(items,page.NextCursor,page.HasMore,token);
        }
    }
    public CatalogPage<CatalogSchema> Schemas(UserAccount user,CatalogRequest request)=>this.CatalogRead(user,request,(type,items,identities)=>Slice(NativeCatalog.Schemas(type).Concat(items.Select(item=>item.Details.Object.Schema)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(name=>new CatalogSchema(name){StableId=identities?.Schema(name),StableIdUnavailableReason=identities?.UnavailableReason("Schema",name)}),request.Cursor,this.CatalogToken(request.VersionToken)));
    public CatalogPage<DefinitionSummary> Queries(UserAccount user,CatalogRequest request){
        lock(this.store.Gate){
            var database=this.CatalogVersion(user,request.DatabaseId)??throw new ManagementException("InvalidRequest","利用者DBを指定してください。");
            var token=this.CatalogToken(request.VersionToken);
            if(request.Cursor<0){throw new ManagementException("InvalidRequest","ページ指定が不正です。");}
            var page=this.store.SelectPage<DefinitionVersion,DefinitionSummary>("active",request.Cursor,100,
                item=>item.Definition.Kind==DefinitionKind.Query&&string.Equals(this.store.CurrentDatabase(item.Definition),database.Database,StringComparison.OrdinalIgnoreCase),
                item=>new(new(item.Definition.Id,database.Database,item.Definition.Kind,item.Definition.EntryType,item.Definition.EntryMember,item.Definition.WritesData),item.Version,item.Owner,item.CreatedAt,item.ArtifactHash));
            return new(page.Items,page.NextCursor,page.HasMore,token);
        }
    }
    public CatalogPage<CatalogObject> Objects(UserAccount user,CatalogRequest request)=>this.CatalogRead(user,request,(_,items)=>Slice(items.Select(item=>item.Details.Object).Where(item=>(request.IncludeSystemObjects||item.Kind!="SystemTable")&&(request.Schema is null||item.Schema==request.Schema)&&(request.Kind is null||item.Kind==request.Kind)&&(string.IsNullOrEmpty(request.NameFilter)||item.Name.Contains(request.NameFilter,StringComparison.OrdinalIgnoreCase))),request.Cursor,this.CatalogToken(request.VersionToken)));
    public CatalogObjectDetails ObjectDetails(UserAccount user,CatalogRequest request)=>this.CatalogRead(user,request,(_,items)=>items.FirstOrDefault(item=>item.Details.Object.Id==request.ObjectId)?.Details??throw new ManagementException("NotFound","オブジェクトがありません。"));
    public CatalogObjectDetails PublishedObjectDetails(UserAccount user,CatalogRequest request){var details=this.ObjectDetails(user,request);return details with{Source=details.Source with{Path=null,Start=0,Length=0,Available=false,UnavailableReason="PublishedMetadata"}};}
    public CatalogObjectSource ObjectSource(UserAccount user,CatalogRequest request){
        lock(this.store.Gate){var version=this.CatalogVersion(user,request.DatabaseId);if(version is not null){RequireSource(user,version);}var details=this.ObjectDetails(user,request);if(!details.Source.Available){version=null;}return new(details.Source,version);}
    }
    private string CatalogToken(string expected){var token=this.store.InstanceId.ToString("N")+":"+this.store.CatalogRevision;if(expected.Length>0&&expected!=token){throw new ManagementException("CatalogChanged","一覧取得後に公開定義または権限が変わりました。一覧を更新してください。");}return token;}
    private DefinitionVersion? CatalogVersion(UserAccount user,string id){
        if(id==SystemDatabaseId){Require(user,"*",ManagementPermission.Manage);return null;}
        if(!id.StartsWith("db:",StringComparison.Ordinal)){throw new ManagementException("InvalidRequest","DB識別情報が不正です。");}
        var version=this.store.Read<DefinitionVersion>("active",id[3..])??throw new ManagementException("NotFound","稼働DBがありません。");Require(user,version.Database,ManagementPermission.View);
        if(version.Definition.Kind!=DefinitionKind.Database){throw new ManagementException("InvalidRequest","DB定義ではありません。");}return version;
    }
    private T CatalogRead<T>(UserAccount user,CatalogRequest request,Func<Type,IEnumerable<NativeCatalog.Item>,T> read){
        return this.CatalogRead(user,request,(type,items,_)=>read(type,items));
    }
    private T CatalogRead<T>(UserAccount user,CatalogRequest request,Func<Type,IEnumerable<NativeCatalog.Item>,CatalogIdentities?,T> read){
        lock(this.store.Gate){user=this.CurrentActor(user);_=this.CatalogToken(request.VersionToken);var version=this.CatalogVersion(user,request.DatabaseId);
            if(version is null){return read(this.store.SystemCatalogType,NativeCatalog.Read(this.store.SystemCatalogType,null),null);}
            using var compiled=this.runtime.ReadArtifact(version);var context=new ManagedLoadContext(imported:compiled.ImportedBuild);
            try{using var stream=new MemoryStream(compiled.AssemblyBytes);var type=context.LoadFromStream(stream).GetType(version.Definition.EntryType,true)!;var identities=DatabaseDesigner.ReadStableIdentities(version.Definition);var items=NativeCatalog.Read(type,version,identities);if(!Can(user,version.Database,ManagementPermission.Edit)){items=items.Select(static item=>item with{Details=item.Details with{Source=item.Details.Source with{Path=null,Start=0,Length=0,Available=false,UnavailableReason="Permission"}}});}return read(type,items,identities);}
            finally{context.Unload();}
        }
    }
    private static CatalogPage<T> Slice<T>(IEnumerable<T> source,long cursor,string token){
        if(cursor<0||cursor>int.MaxValue){throw new ManagementException("InvalidRequest","ページ指定が不正です。");}
        var items=source.Skip((int)cursor).Take(101).ToArray();var more=items.Length>100;return new(items.Take(100).ToArray(),cursor+Math.Min(100,items.Length),more,token);
    }
}
