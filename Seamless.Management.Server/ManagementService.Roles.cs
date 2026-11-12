using System.Collections.Frozen;
using Seamless.Security;

namespace Seamless.Management.Server;

public sealed partial class ManagementService{
    private readonly object commonAuthorizationGate=new();
    private StoredAuthorization? commonAuthorization;
    internal async Task<T> ManageAuthorizationAsync<T>(UserAccount actor,string operation,object payload,CancellationToken cancellation){
        if(operation is not("authorizationDatabases" or "authorizationDatabase" or "authorizationChange" or "authorizationScope" or "authorizationScopeChange" or "authorizationOperation")){
            throw new ManagementException("Unsupported","対応していない共通認可の管理操作です。");
        }
        lock(this.store.Gate){actor=this.CurrentActor(actor);Require(actor,"*",ManagementPermission.Manage);}
        StoredAuthorization policy;
        lock(this.commonAuthorizationGate){ObjectDisposedException.ThrowIf(this.disposed,this);policy=this.commonAuthorization??=StoredAuthorization.Open(this.store);}
        var prefix="management-user:";if(actor.PasswordHash.Length==0){prefix="os-owner:";}
        var subject=new SecuritySubjectId(this.store.InstanceId,prefix+actor.Name);subject.Validate();
        using var lease=policy.ManagementOperation(subject,cancellation);
        var api=new ManagementAuthorizationApi(policy,null,null,null,null,false);
        try{return(T)await api.InvokeAuthorizedAsync(new(Guid.NewGuid(),operation,null,"system",ManagementJson.Element(payload)),lease).ConfigureAwait(false);}
        catch(AuthorizationException error){throw new ManagementException(error.Code,error.Message);}
        catch(ArgumentException error){throw new ManagementException("Validation",error.Message);}
    }
    private void DisposeCommonAuthorization(){lock(this.commonAuthorizationGate){this.commonAuthorization?.Dispose();}}
}

internal sealed partial class StoredAuthorization{
    internal AuthorizationLease ManagementOperation(SecuritySubjectId subject,CancellationToken cancellation)=>this.WithPolicy(()=>{
        this.manager.EnsureActive();subject.Validate();var current=this.manager.Policy;
        // 本人確認済みの所有者、または全DBの管理権限を検査した管理入口だけが作成する。
        // 保存ポリシーへ一時的なロールを追加せず、操作監査と版照合には同じマネージャーを使う。
        var permissions=new Dictionary<Guid,(SecurityPermission,SecurityPermission)>{{current.ServerId,(SecurityPermission.All,SecurityPermission.None)}}.ToFrozenDictionary();
        var now=DateTimeOffset.UtcNow;var snapshot=new AuthorizationSnapshot(current,subject,now,[],permissions);
        return new AuthorizationLease(this.manager,snapshot,now,null,CancellationToken.None,cancellation);
    });
}
