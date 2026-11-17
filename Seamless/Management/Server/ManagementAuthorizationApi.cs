using Seamless.Authentication;
using Seamless.Security;
using System.Text.Json;

namespace Seamless.Management.Server;

/// <summary>ストアを所有するホストの共通認可を、既存の管理RPCへ接続する。</summary>
internal sealed class ManagementAuthorizationApi(StoredAuthorization policy,OidcTokenVerifier? tokens,
    Func<GatewayPublicationRequest,AuthorizationLease,GatewayPublicationIdentity>? gatewayPublication,
    Func<GatewayPublicationListRequest,AuthorizationLease,GatewayPublicationList>? gatewayPublications,
    Func<GatewayPublicationApprovalRequest,AuthorizationLease,bool,GatewayPublicationApprovalResult>? gatewayApproval,bool gatewayApprovalRegistration){
    internal ServerInfo Info{
        get{
            var capabilities=new List<string>{"AuthorizationManagementV1","AuthorizationSubjectStatusV1","AuthorizationDatabaseListV1","AuthorizationScopesV1"};
            if(gatewayPublication is not null){capabilities.Add("GatewayPublicationIdentityV1");}
            if(gatewayPublications is not null){capabilities.Add("GatewayPublicationListV1");}
            if(gatewayApproval is not null){capabilities.Add("GatewayApprovalExportV1");}
            if(gatewayApprovalRegistration){capabilities.Add("GatewayApprovalRegistrationV1");}
            return new(policy.Policy.ServerId,"1",System.Runtime.InteropServices.RuntimeInformation.OSDescription,capabilities.ToArray());
        }
    }
    internal async Task<object> InvokeAsync(RpcRequest request,CancellationToken cancellation){
        var supported=request.Operation switch{
            "authorizationDatabases" or "authorizationDatabase" or "authorizationChange" or "authorizationScope" or "authorizationScopeChange" or "authorizationOperation" or "authorizationSubjectChange" or "authorizationSubjectOperation"=>true,
            "gatewayPublication"=>gatewayPublication is not null,
            "gatewayPublications"=>gatewayPublications is not null,
            "gatewayApprovalExport"=>gatewayApproval is not null,
            "gatewayApprovalRegister"=>gatewayApproval is not null&&gatewayApprovalRegistration,
            _=>false
        };
        if(!supported){
            throw new ManagementException("Unsupported","このリスナーはこの管理操作に対応していません。");
        }
        var identity=await (tokens??throw new InvalidOperationException("OIDCの本人確認が接続されていません。")).AuthenticateAsync(request.Token??"",cancellation).ConfigureAwait(false);
        using var session=policy.CreateSession(identity);
        using var operation=session.BeginOperation(cancellation);
        return await this.InvokeAuthorizedAsync(request,operation).ConfigureAwait(false);
    }
    internal async Task<object> InvokeAuthorizedAsync(RpcRequest request,AuthorizationLease operation){
        if(request.Operation=="authorizationSubjectChange"){
            var change=ManagementJson.Read<AuthorizationSubjectChange>(request.Payload);
            if(change.ExpectedInstanceId is{} instance&&instance!=policy.Policy.ServerId){
                throw new ManagementException("Connection","組織アカウントの変更先が接続プロファイルと一致しません。");
            }
            StoredSubjectStatus result;
            try{result=await policy.ApplySubjectStatusAsync(operation,change.Subject,change.Disabled,change.OperationId).ConfigureAwait(false);}
            catch(ArgumentException){throw new ManagementException("Validation","アカウント変更の操作IDと主体が必要です。");}
            return SubjectResult(result);
        }
        if(request.Operation=="authorizationSubjectOperation"){
            var id=ManagementJson.Read<Guid>(request.Payload);
            var result=policy.FindSubjectStatusOperation(id);
            if(result is null||result.AppliedBy.Subject!=operation.Audit.Subject){throw new ManagementException("NotFound","本人の操作結果がありません。");}
            return SubjectResult(result);
        }
        if(request.Operation is "gatewayApprovalExport" or "gatewayApprovalRegister"){
            var approval=ManagementJson.Read<GatewayPublicationApprovalRequest>(request.Payload);
            var register=request.Operation=="gatewayApprovalRegister";
            if(register){operation.Demand(policy.Policy.ServerId,SecurityPermission.ManageServer);}
            if(approval.Publication is not{} publication||publication.ExpectedGenerationId is null||approval.QueryResourceId==Guid.Empty||
                approval.Access is null||approval.Access.Count>1024||approval.Access.Any(static item=>item is null)){
                throw new ManagementException("Validation","公開世代・実行資源と、最大1024件のアクセス指定が必要です。");
            }
            DemandPublication(publication,operation);
            return gatewayApproval!(approval,operation,register);
        }
        if(request.Operation=="gatewayPublications"){
            var query=ManagementJson.Read<GatewayPublicationListRequest>(request.Payload);
            if(query.PageSize is <1 or >100||query.After is{} cursor&&(cursor.CatalogVersion==Guid.Empty||cursor.PolicyVersion<0||
                cursor.Scope is not{Length:64}||string.IsNullOrWhiteSpace(cursor.ModuleId)||cursor.ModuleId.Length>1024||
                string.IsNullOrWhiteSpace(cursor.QueryId)||cursor.QueryId.Length>1024)){
                throw new ManagementException("Validation","公開一覧のページまたはカーソルが不正です。");
            }
            return gatewayPublications!(query,operation);
        }
        if(request.Operation=="gatewayPublication"){
            var publication=ManagementJson.Read<GatewayPublicationRequest>(request.Payload);
            DemandPublication(publication,operation);
            return gatewayPublication!(publication,operation);
        }
        if(request.Operation=="authorizationDatabase"){
            return ReadDatabase(ManagementJson.Read<DatabaseAuthorizationQuery>(request.Payload),operation);
        }
        if(request.Operation=="authorizationScope"){
            return this.ReadScope(ManagementJson.Read<AuthorizationScopeQuery>(request.Payload),operation);
        }
        if(request.Operation=="authorizationDatabases"){
            return this.ReadDatabases(ManagementJson.Read<DatabaseAuthorizationListQuery>(request.Payload),operation);
        }
        if(request.Operation is "authorizationChange" or "authorizationScopeChange"){
            DatabaseAuthorizationEdit edit;
            if(request.Operation=="authorizationScopeChange"){edit=ManagementJson.Read<AuthorizationScopeEdit>(request.Payload).DatabaseEdit();}
            else{edit=ManagementJson.Read<DatabaseAuthorizationEdit>(request.Payload);}
            if(edit.OperationId==Guid.Empty||edit.UpsertRoles is null||edit.RemoveRoles is null||edit.Assign is null||edit.Unassign is null
                ||edit.UpsertRoles.Any(static role=>role is null||role.Rules.IsDefault||role.Rules.Any(static rule=>rule is null))
                ||edit.Assign.Any(static binding=>binding is null)||edit.Unassign.Any(static binding=>binding is null)){
                throw new ManagementException("Validation","認可の変更要求が不正です。");
            }
            var roles=edit.UpsertRoles.Select(static role=>new SecurityRole(role.Id,role.Name,role.Rules));
            if(request.Operation=="authorizationScopeChange"){policy.ApplyRoleChange(operation,new(edit.DatabaseId,edit.ExpectedVersion,roles,edit.RemoveRoles,edit.Assign,edit.Unassign),edit.OperationId);}
            else{policy.ApplyDatabaseChange(operation,new(edit.DatabaseId,edit.ExpectedVersion,roles,edit.RemoveRoles,edit.Assign,edit.Unassign),edit.OperationId);}
            return Result(edit.OperationId,operation.Audit.Subject);
        }
        return Result(ManagementJson.Read<Guid>(request.Payload),operation.Audit.Subject);
    }
    private static void DemandPublication(GatewayPublicationRequest publication,AuthorizationLease operation){
        if(publication.DatabaseResourceId==Guid.Empty||string.IsNullOrWhiteSpace(publication.ModuleId)||publication.ModuleId.Length>1024||
            publication.ModuleId.StartsWith('$')||string.IsNullOrWhiteSpace(publication.QueryId)||publication.QueryId.Length>1024||publication.ExpectedGenerationId==Guid.Empty){
            throw new ManagementException("Validation","DB・公開モジュール・クエリを指定してください。");
        }
        operation.Demand(publication.DatabaseResourceId,SecurityPermission.ManagePermissions);
        if(operation.Snapshot.Resource(publication.DatabaseResourceId)?.Kind!=SecurityResourceKind.Database){throw new ManagementException("Validation","DBの対象IDが必要です。");}
    }
    private DatabaseAuthorizationInfo ReadDatabase(DatabaseAuthorizationQuery query,AuthorizationLease operation){
        var result=this.ReadScope(new(query.DatabaseId,query.Version,query.Cursor),operation,true);
        return new(result.Version,result.ScopeId,result.Items,result.NextCursor,result.HasMore);
    }
    private AuthorizationScopeInfo ReadScope(AuthorizationScopeQuery query,AuthorizationLease operation,bool databaseOnly=false){
        ValidatePageRequest(query.Version,query.Cursor);
        var(current,document)=policy.CurrentView();
        var kind=current.Resource(query.ScopeId)?.Kind;var server=kind==SecurityResourceKind.Server&&!databaseOnly;
        if(server){operation.Demand(current.ServerId,SecurityPermission.ManageServer);}else{operation.Demand(query.ScopeId,SecurityPermission.ManagePermissions);}
        if(kind is not(SecurityResourceKind.Database or SecurityResourceKind.Server)||databaseOnly&&kind!=SecurityResourceKind.Database){throw new ManagementException("Validation","対象のDBまたはサーバー資源が必要です。");}
        if(query.Version!=0&&query.Version!=current.Version){throw new ManagementException("Conflict","権限の版が変わりました。最初から取得してください。");}
        var ids=document.Roles.Where(role=>server||!role.Rules.IsEmpty&&role.Rules.All(rule=>current.IsWithin(rule.ResourceId,query.ScopeId))).Select(static role=>role.Id).ToHashSet();
        var(items,nextCursor,hasMore)=ReadPage(Entries(),query.Cursor,operation);
        return new(current.Version,query.ScopeId,items,nextCursor,hasMore);
        IEnumerable<AuthorizationMetadataEntry> Entries(){
            foreach(var resource in document.Resources){if(current.IsWithin(resource.Id,query.ScopeId)){yield return new(Resource:resource);}}
            foreach(var role in document.Roles){if(!ids.Contains(role.Id)){continue;}yield return new(Role:new(role.Id,role.Name));foreach(var rule in role.Rules){yield return new(Rule:new(role.Id,rule));}}
            foreach(var binding in document.Bindings){if(ids.Contains(binding.RoleId)){yield return new(Binding:binding);}}
        }
    }
    private DatabaseAuthorizationList ReadDatabases(DatabaseAuthorizationListQuery query,AuthorizationLease operation){
        ValidatePageRequest(query.Version,query.Cursor);
        var(current,document)=policy.CurrentView();
        if(current.Version!=operation.Snapshot.PolicyVersion||query.Version!=0&&query.Version!=current.Version){
            throw new ManagementException("Conflict","権限の版が変わりました。最初から取得してください。");
        }
        var(items,nextCursor,hasMore)=ReadPage(Entries(),query.Cursor,operation);
        return new(current.Version,items.Select(static item=>item.Resource!).ToArray(),nextCursor,hasMore);
        IEnumerable<AuthorizationMetadataEntry> Entries(){
            foreach(var resource in document.Resources){
                operation.ThrowIfCancellationRequested();
                if(resource.Kind==SecurityResourceKind.Database&&operation.Snapshot.Evaluate(resource.Id,SecurityPermission.ManagePermissions).Allowed){
                    yield return new(Resource:resource);
                }
            }
        }
    }
    private static void ValidatePageRequest(long version,long cursor){
        if(cursor<0||version<0||cursor>0&&version==0){throw new ManagementException("Validation","続きの取得には版と有効なカーソルが必要です。");}
    }
    private static (AuthorizationMetadataEntry[] Items,long NextCursor,bool HasMore) ReadPage(
        IEnumerable<AuthorizationMetadataEntry> source,long start,AuthorizationLease operation){
        using var entries=source.GetEnumerator();long cursor=0;
        while(cursor<start){operation.ThrowIfCancellationRequested();if(!entries.MoveNext()){throw new ManagementException("Validation","カーソルが取得範囲を超えています。");}cursor++;}
        var items=new List<AuthorizationMetadataEntry>(100);var bytes=0;var more=false;
        while(entries.MoveNext()){
            operation.ThrowIfCancellationRequested();
            if(items.Count==100){more=true;break;}
            if(entries.Current.Resource?.Name.Length>512*1024||entries.Current.Role?.Name.Length>512*1024){throw new ManagementException("Limit","一つの権限項目が応答容量の上限を超えています。");}
            var size=JsonSerializer.SerializeToUtf8Bytes(entries.Current,ManagementJson.Options).Length;
            if(bytes+size>512*1024){if(items.Count==0){throw new ManagementException("Limit","一つの権限項目が応答容量の上限を超えています。");}more=true;break;}
            items.Add(entries.Current);bytes+=size;cursor++;
        }
        return (items.ToArray(),cursor,more);
    }
    private AuthorizationChangeResult Result(Guid id,SecuritySubjectId subject){
        var result=policy.FindOperation(id);
        if(result is null||result.AppliedBy.Subject!=subject){throw new ManagementException("NotFound","本人の操作結果がありません。");}
        return new(id,result.Version,result.Hash,result.CommittedAt,result.DatabaseId);
    }
    private static AuthorizationSubjectChangeResult SubjectResult(StoredSubjectStatus status)=>new(status.OperationId,status.Subject,status.Disabled,status.CommittedAt);
}
