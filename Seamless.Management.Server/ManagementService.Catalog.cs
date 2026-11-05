namespace Seamless.Management.Server;

public sealed partial class ManagementService{
    public const string SystemDatabaseId="system";
    public CatalogPage<CatalogDatabase> Databases(UserAccount user,long cursor=0,string versionToken=""){
        lock(this.store.Gate){var token=this.CatalogToken(versionToken);if(cursor<0){throw new ManagementException("InvalidRequest","ページ指定が不正です。");}
            var page=this.store.SelectPage<DefinitionVersion,CatalogDatabase>("active",cursor,100,item=>item.Definition.Kind==DefinitionKind.Database&&Can(user,item.Definition.Database,ManagementPermission.View),item=>new("db:"+item.Definition.Id,item.Definition.Database,item.Definition.Id,item.Version,item.ArtifactHash,false));
            var items=page.Items;if(cursor==0&&Can(user,"*",ManagementPermission.Manage)){items=[new(SystemDatabaseId,"Seamless システムDB","",0,"",true),..items];}
            return new(items,page.NextCursor,page.HasMore,token);
        }
    }
    public CatalogPage<CatalogSchema> Schemas(UserAccount user,CatalogRequest request)=>this.CatalogRead(user,request,(type,items)=>Slice(NativeCatalog.Schemas(type).Concat(items.Select(item=>item.Details.Object.Schema)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(name=>new CatalogSchema(name)),request.Cursor,this.CatalogToken(request.VersionToken)));
    public CatalogPage<CatalogObject> Objects(UserAccount user,CatalogRequest request)=>this.CatalogRead(user,request,(_,items)=>Slice(items.Select(item=>item.Details.Object).Where(item=>(request.Schema is null||item.Schema==request.Schema)&&(request.Kind is null||item.Kind==request.Kind)&&(string.IsNullOrEmpty(request.NameFilter)||item.Name.Contains(request.NameFilter,StringComparison.OrdinalIgnoreCase))),request.Cursor,this.CatalogToken(request.VersionToken)));
    public CatalogObjectDetails ObjectDetails(UserAccount user,CatalogRequest request)=>this.CatalogRead(user,request,(_,items)=>items.FirstOrDefault(item=>item.Details.Object.Id==request.ObjectId)?.Details??throw new ManagementException("NotFound","オブジェクトがありません。"));
    public CatalogObjectSource ObjectSource(UserAccount user,CatalogRequest request){
        lock(this.store.Gate){var details=this.ObjectDetails(user,request);var version=details.Source.Available?this.CatalogVersion(user,request.DatabaseId):null;return new(details.Source,version);}
    }
    private string CatalogToken(string expected){var token=this.store.InstanceId.ToString("N")+":"+this.store.Revision;if(expected.Length>0&&expected!=token){throw new ManagementException("CatalogChanged","一覧取得後にサーバーの版が変わりました。一覧を更新してください。");}return token;}
    private DefinitionVersion? CatalogVersion(UserAccount user,string id){
        if(id==SystemDatabaseId){Require(user,"*",ManagementPermission.Manage);return null;}
        if(!id.StartsWith("db:",StringComparison.Ordinal)){throw new ManagementException("InvalidRequest","DB識別情報が不正です。");}
        var version=this.store.Read<DefinitionVersion>("active",id[3..])??throw new ManagementException("NotFound","稼働DBがありません。");Require(user,version.Definition.Database,ManagementPermission.View);
        if(version.Definition.Kind!=DefinitionKind.Database){throw new ManagementException("InvalidRequest","DB定義ではありません。");}return version;
    }
    private T CatalogRead<T>(UserAccount user,CatalogRequest request,Func<Type,IEnumerable<NativeCatalog.Item>,T> read){
        lock(this.store.Gate){_=this.CatalogToken(request.VersionToken);var version=this.CatalogVersion(user,request.DatabaseId);
            if(version is null){return read(typeof(ManagementDatabase),NativeCatalog.Read(typeof(ManagementDatabase),null));}
            var compiled=this.runtime.ReadArtifact(version);var context=new ManagedLoadContext();
            try{using var stream=new MemoryStream(compiled.AssemblyBytes);var type=context.LoadFromStream(stream).GetType(version.Definition.EntryType,true)!;return read(type,NativeCatalog.Read(type,version));}
            finally{context.Unload();}
        }
    }
    private static CatalogPage<T> Slice<T>(IEnumerable<T> source,long cursor,string token){
        if(cursor<0||cursor>int.MaxValue){throw new ManagementException("InvalidRequest","ページ指定が不正です。");}
        var items=source.Skip((int)cursor).Take(101).ToArray();var more=items.Length>100;return new(items.Take(100).ToArray(),cursor+Math.Min(100,items.Length),more,token);
    }
}
