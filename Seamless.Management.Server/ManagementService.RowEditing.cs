namespace Seamless.Management.Server;

public sealed partial class ManagementService{
    private const ManagementPermission LegacyAdministrator=ManagementPermission.View|ManagementPermission.Execute|ManagementPermission.Edit|
        ManagementPermission.Validate|ManagementPermission.Apply|ManagementPermission.Manage;

    public Task<TableRows> ReadRowsAsync(UserAccount user,TableRowsRequest request,CancellationToken cancellation=default){
        var id=this.AuthorizeRowDatabase(user,request.Database,ManagementPermission.View|ManagementPermission.ReadRows,true);
        this.AfterRowAuthorization?.Invoke();return this.runtime.ReadRowsAsync(request,id,cancellation,()=>{if(this.AuthorizeRowDatabase(user,request.Database,ManagementPermission.View|ManagementPermission.ReadRows,true)!=id){throw new ManagementException("CatalogChanged","行取得のDB識別が変わりました。");}});
    }

    public Task<ObjectExecutionResult> ExecuteObjectAsync(UserAccount user,ObjectExecutionRequest request,CancellationToken cancellation=default){
        var id=this.AuthorizeRowDatabase(user,request.Database,ManagementPermission.View|ManagementPermission.ReadRows|ManagementPermission.Execute,false);
        if(request.ResourceHeatOptions is not null){this.AuthorizeResourceHeat(user);}
        if(request.MeasurementMode==ObjectExecutionMeasurementMode.Profiled){this.AuthorizeProfile(user,id);}
        Action authorize=()=>{if(this.AuthorizeRowDatabase(user,request.Database,ManagementPermission.View|ManagementPermission.ReadRows|ManagementPermission.Execute,false)!=id){throw new ManagementException("CatalogChanged","問い合わせのDB識別が変わりました。");}if(request.MeasurementMode==ObjectExecutionMeasurementMode.Profiled){this.AuthorizeProfile(user,id);}};
        this.AfterRowAuthorization?.Invoke();var result=this.runtime.ExecuteObjectAsync(request,id,cancellation,authorize);
        if(request.ResourceHeatOptions is not null){result=this.ResourceHeatResultAsync(user,result);}
        if(request.MeasurementMode==ObjectExecutionMeasurementMode.Profiled){result=this.ProfileResultAsync(user,id,result);}return result;
    }
    private void AuthorizeResourceHeat(UserAccount user){lock(this.store.Gate){Require(this.CurrentActor(user),"*",ManagementPermission.Manage);}}
    private async Task<ObjectExecutionResult> ResourceHeatResultAsync(UserAccount user,Task<ObjectExecutionResult> pending){var result=await pending.ConfigureAwait(false);this.AuthorizeResourceHeat(user);return result;}

    public Task<TableRowCommit> CommitRowsAsync(UserAccount user,TableRowBatch request,CancellationToken cancellation=default){
        var id=this.AuthorizeRowDatabase(user,request.Database,ManagementPermission.View|ManagementPermission.ReadRows|ManagementPermission.WriteRows,true);
        this.AfterRowAuthorization?.Invoke();return this.runtime.CommitRowsAsync(request,id,user,cancellation);
    }

    internal Action? AfterRowAuthorization{get;set;}
    private string AuthorizeRowDatabase(UserAccount user,string database,ManagementPermission permissions,bool legacy){
        lock(this.store.Gate){
            var actor=this.CurrentActor(user);
            if(legacy){RequireRowDatabase(actor,database,permissions);}else{Require(actor,database,permissions);}
            return this.store.DatabaseDefinition(database).Definition.Id;
        }
    }

    private static void RequireRowDatabase(UserAccount user,string database,ManagementPermission permissions){
        if(string.IsNullOrWhiteSpace(database)){throw new ManagementException("InvalidRequest","DB名が必要です。");}
        if(database.Equals("system",StringComparison.OrdinalIgnoreCase)){throw new ManagementException("Permission","システムDBの行は管理用テーブル操作の対象外です。");}
        if(!Can(user,"*",LegacyAdministrator)){Require(user,database,permissions);}
    }
}
