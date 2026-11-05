using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace Seamless.Management.Server;

internal sealed record ManagementSession(string User,DateTimeOffset ExpiresAt,string CredentialHash);

public sealed partial class ManagementService:IDisposable{
    private readonly ManagementStore store;
    private readonly NativeRuntime runtime;
    private readonly ConcurrentDictionary<string,ManagementSession> sessions=new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string,(int Failures,DateTimeOffset Until)> failures=new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid,CancellationTokenSource> running=new();
    private readonly PasswordHasher<UserAccount> hasher=new();
    private bool disposed;
    internal Action<string,Guid>? PublicationCheckpoint{get;set;}
    internal Action? BeforeQueryAdmission{get=>this.runtime.BeforeQueryAdmission;set=>this.runtime.BeforeQueryAdmission=value;}
    internal WeakReference? LastQueryContext=>this.runtime.LastQueryContext;
    public ManagementService(ManagementStore store){this.store=store;this.runtime=new(store);}
    public ServerInfo Info=>new(this.store.InstanceId,"1",System.Runtime.InteropServices.RuntimeInformation.OSDescription,["NativeManagementV1","StaticPlanV1","PersonalDrafts","ValidationDatabase","ExplicitApply","NativeCatalogV1"]);
    public LoginResult Login(LoginRequest request){
        ObjectDisposedException.ThrowIf(this.disposed,this);ManagementStore.ValidateAccountName(request.Name);
        var name=ManagementStore.Normalize(request.Name);var now=DateTimeOffset.UtcNow;
        foreach(var session in this.sessions){if(session.Value.ExpiresAt<=now){this.sessions.TryRemove(session.Key,out _);}}
        foreach(var entry in this.failures){if(entry.Value.Until.AddMinutes(5)<=now){this.failures.TryRemove(entry.Key,out _);}}
        if(this.sessions.Count>=1024||this.failures.Count>=1024&&!this.failures.ContainsKey(name)){throw new ManagementException("RateLimited","接続数の上限です。");}
        if(this.failures.TryGetValue(name,out var failure)&&failure.Until>now){throw new ManagementException("RateLimited","ログインの再試行をしばらく待ってください。");}
        var user=this.store.Read<UserAccount>("user",name);
        if(user is null||user.Disabled||this.hasher.VerifyHashedPassword(user,user.PasswordHash,request.Password)==PasswordVerificationResult.Failed){
            var count=failure.Failures+1;var until=now;if(count>=5){until=now.AddMinutes(5);count=0;}this.failures[name]=(count,until);throw new ManagementException("Authentication","利用者名またはパスワードを確認してください。");
        }
        this.failures.TryRemove(name,out _);var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));var expires=now.AddMinutes(30);
        this.sessions[TokenHash(token)]=new(name,expires,user.PasswordHash);return new(token,expires,new(user.Name,user.Grants,user.Disabled));
    }
    public UserAccount Authenticate(string? token){
        ObjectDisposedException.ThrowIf(this.disposed,this);
        if(token is null||!this.sessions.TryGetValue(TokenHash(token),out var session)||session.ExpiresAt<=DateTimeOffset.UtcNow){throw new ManagementException("Authentication","ログインが必要です。");}
        var user=this.store.Read<UserAccount>("user",session.User);if(user is null||user.Disabled||user.PasswordHash!=session.CredentialHash){throw new ManagementException("Authentication","利用者またはセッションが無効です。再度ログインしてください。");}return user;
    }
    internal UserAccount AuthenticateLocal(string name){
        ObjectDisposedException.ThrowIf(this.disposed,this);ManagementStore.ValidateAccountName(name);
        var user=this.store.Read<UserAccount>("user",ManagementStore.Normalize(name));if(user is null||user.Disabled){throw new ManagementException("Authentication","ローカル接続用の利用者が登録されていないか、無効です。起動設定のLocalUserを確認してください。");}return user;
    }
    public static bool Can(UserAccount user,string database,ManagementPermission permission)=>user.Grants.Any(grant=>(grant.Database==database||grant.Database=="*")&&(grant.Permissions&permission)==permission);
    private static void Require(UserAccount user,string database,ManagementPermission permission){if(!Can(user,database,permission)){throw new ManagementException("Permission","このDBに対する操作権限がありません。");}}
    public Page<UserInfo> Users(UserAccount user,long cursor=0){Require(user,"*",ManagementPermission.Manage);var page=this.store.List<UserAccount>("user",cursor);return new(page.Items.Select(item=>new UserInfo(item.Name,item.Grants,item.Disabled)).ToArray(),page.NextCursor,page.HasMore);}
    public void ChangeUser(UserAccount actor,ChangeUserRequest change){
        Require(actor,"*",ManagementPermission.Manage);ManagementStore.ValidateAccountName(change.Name);var key=ManagementStore.Normalize(change.Name);
        if(change.Grants.Any(grant=>string.IsNullOrWhiteSpace(grant.Database)||(grant.Permissions&~ManagementPermission.All)!=0)){throw new ManagementException("Validation","権限指定が不正です。");}
        if(key==ManagementStore.Normalize(actor.Name)&&(change.Disabled||!change.Grants.Any(grant=>grant.Database=="*"&&(grant.Permissions&ManagementPermission.Manage)!=0))){throw new ManagementException("Validation","自身の管理権限を解除する操作は受け付けません。");}
        lock(this.store.Gate){var previous=this.store.Read<UserAccount>("user",key);var updated=new UserAccount(change.Name,previous?.PasswordHash??"",change.Grants,change.Disabled);
            if(change.Password is not null){if(change.Password.Length<12){throw new ManagementException("Validation","パスワードは12文字以上にしてください。");}updated=updated with{PasswordHash=this.hasher.HashPassword(updated,change.Password)};}
            if(updated.PasswordHash.Length==0){throw new ManagementException("Validation","新しい利用者にはパスワードが必要です。");}
            this.store.Write(()=>{this.store.Put("user",key,updated);this.store.AddHistory(actor.Name,"*","UserChanged",change.Name,0);});
            if(change.Password is not null||change.Disabled){foreach(var session in this.sessions){if(ManagementStore.Normalize(session.Value.User)==key){this.sessions.TryRemove(session.Key,out _);}}}
        }
    }
    public void ChangePassword(UserAccount actor,ChangePasswordRequest change){
        Require(actor,"*",ManagementPermission.Manage);ManagementStore.ValidateAccountName(change.Name);
        lock(this.store.Gate){var account=this.store.Read<UserAccount>("user",ManagementStore.Normalize(change.Name))??throw new ManagementException("NotFound","利用者が存在しません。");this.ChangeUser(actor,new(account.Name,change.Password,account.Grants,account.Disabled));}
    }
    public Draft SaveDraft(UserAccount user,SaveDraftRequest request){
        var definition=request.Definition;ValidateDefinition(definition);Require(user,definition.Database,ManagementPermission.Edit);
        lock(this.store.Gate){var key=DraftSubject(user.Name,definition.Id);var previous=this.store.Read<Draft>("draft",key);
            if((previous?.Revision??0)!=request.ExpectedRevision){throw new ManagementException("Conflict","下書きが別の操作で更新されました。");}
            var active=this.store.Read<DefinitionVersion>("active",definition.Id);
            if(active is not null&&(active.Definition.Database!=definition.Database||active.Definition.Kind!=definition.Kind)){throw new ManagementException("Conflict","既存の定義のDB・種類は変更できません。");}
            if(definition.Kind==DefinitionKind.Database&&this.store.List<DefinitionVersion>("active",predicate:item=>item.Definition.Kind==DefinitionKind.Database&&item.Definition.Database==definition.Database&&item.Definition.Id!=definition.Id).Items.Length>0){throw new ManagementException("Conflict","このDBには既に別のDB定義があります。");}
            var draft=new Draft(definition,user.Name,checked(request.ExpectedRevision+1),request.BaseVersion,DateTimeOffset.UtcNow);
            this.store.Write(()=>{this.store.Put("draft",key,draft);this.store.AddHistory(user.Name,definition.Database,"DraftSaved",definition.Id,draft.Revision);});return draft;
        }
    }
    public Draft GetDraft(UserAccount user,string id,long? revision=null){
        var draft=this.store.Read<Draft>("draft",DraftSubject(user.Name,id))??throw new ManagementException("NotFound","個人の下書きがありません。");
        Require(user,draft.Definition.Database,ManagementPermission.Edit);if(revision.HasValue&&draft.Revision!=revision){throw new ManagementException("Conflict","下書きの版が変わっています。");}return draft;
    }
    public Page<DefinitionSummary> Definitions(UserAccount user,long cursor=0)=>this.store.SelectPage<DefinitionVersion,DefinitionSummary>("active",cursor,100,item=>Can(user,item.Definition.Database,ManagementPermission.View),item=>new(new(item.Definition.Id,item.Definition.Database,item.Definition.Kind,item.Definition.EntryType,item.Definition.EntryMember,item.Definition.WritesData),item.Version,item.Owner,item.CreatedAt,item.ArtifactHash));
    public Page<DraftSummary> Drafts(UserAccount user,long cursor=0)=>this.store.SelectPage<Draft,DraftSummary>("draft",cursor,100,item=>ManagementStore.Normalize(item.Owner)==ManagementStore.Normalize(user.Name)&&Can(user,item.Definition.Database,ManagementPermission.Edit),item=>new(new(item.Definition.Id,item.Definition.Database,item.Definition.Kind,item.Definition.EntryType,item.Definition.EntryMember,item.Definition.WritesData),item.Revision,item.BaseVersion,item.SavedAt));
    public DefinitionVersion GetDefinition(UserAccount user,string id){var definition=this.store.Read<DefinitionVersion>("active",id)??throw new ManagementException("NotFound","定義がありません。");Require(user,definition.Definition.Database,ManagementPermission.View);return definition;}
    public BuildResult Build(UserAccount user,DraftKey key){var draft=this.GetDraft(user,key.Id,key.Revision);Require(user,draft.Definition.Database,ManagementPermission.Validate);return this.runtime.Compile(draft.Definition).Result;}
    public AnalysisResult Analyze(UserAccount user,Definition definition){ValidateDefinition(definition);Require(user,definition.Database,ManagementPermission.Edit|ManagementPermission.Validate);return this.runtime.Analyze(definition);}
    public DefinitionVersion Version(UserAccount user,string id,long version){var value=this.store.Read<DefinitionVersion>("version",id+":"+version)??throw new ManagementException("NotFound","指定した版がありません。");Require(user,value.Definition.Database,ManagementPermission.View);return value;}
    internal void RequestStop(){foreach(var item in this.running.Values){item.Cancel();}}
    public ChangePreview Preview(UserAccount user,DraftKey key){
        var draft=this.GetDraft(user,key.Id,key.Revision);Require(user,draft.Definition.Database,ManagementPermission.Apply);
        var active=this.store.Read<DefinitionVersion>("active",key.Id);var build=this.runtime.Compile(draft.Definition);string[] changes;DiagnosticInfo? migrationError=null;
        try{changes=this.runtime.DescribeChanges(active?.Definition,draft.Definition);}catch(ManagementException error){changes=[error.Message];migrationError=new(error.Code,error.Message);}
        var dependencies=this.runtime.Dependencies(draft.Definition);var version=active?.Version??0;
        var diagnostics=build.Result.Diagnostics.Concat(this.runtime.ValidateDependents(draft.Definition,build)).ToList();if(migrationError is not null){diagnostics.Add(migrationError);}if(version!=draft.BaseVersion){diagnostics.Add(new("Conflict","共有版が進んでいます。差分を確認し基準版を更新してください。"));}
        var validation=this.store.Read<ValidationResult>("lastValidation",DraftSubject(user.Name,key.Id));if(validation is null||validation.DefinitionHash!=draft.Definition.Hash()||validation.DraftRevision!=draft.Revision){validation=null;diagnostics.Add(new("ValidationNotRun","この下書き版の検証結果はまだありません。",Severity:"Warning"));}else if(!validation.Passed){diagnostics.Add(new("ValidationFailed","直近の検証は失敗しています: "+validation.Error,Severity:"Warning"));}
        var token=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(draft.Definition.Hash()+"\n"+draft.Revision+"\n"+version+"\n"+string.Join("\n",dependencies))));
        return new(token,key.Id,key.Revision,version,changes,dependencies,diagnostics.All(item=>item.Severity!="Error"),diagnostics.ToArray(),validation);
    }
    public OperationInfo Apply(UserAccount user,ApplyRequest request){
        var intent=Convert.ToHexString(SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(request,ManagementJson.Options)));
        lock(this.store.Gate){var existing=this.store.Read<OperationInfo>("operation",request.OperationId.ToString("N"));if(existing is not null){Require(user,existing.Database,ManagementPermission.Apply);if(existing.Owner!=user.Name||existing.IntentHash!=intent){throw new ManagementException("Conflict","操作IDは別の要求に使用されています。");}return existing;}}
        var draft=this.GetDraft(user,request.Id,request.DraftRevision);Require(user,draft.Definition.Database,ManagementPermission.Apply);
        lock(this.store.Gate){var existing=this.store.Read<OperationInfo>("operation",request.OperationId.ToString("N"));if(existing is not null){if(existing.Owner!=user.Name||existing.IntentHash!=intent){throw new ManagementException("Conflict","操作IDは別の要求に使用されています。");}return existing;}}
        var preview=this.Preview(user,new(request.Id,request.DraftRevision));if(!preview.CanApply||preview.Token!=request.PreviewToken){throw new ManagementException("Conflict","変更影響を再確認してください。");}
        var operation=new OperationInfo(request.OperationId,user.Name,draft.Definition.Database,request.Id,OperationState.Running,null,null,DateTimeOffset.UtcNow,intent);
        using var cancellation=new CancellationTokenSource();if(!this.running.TryAdd(operation.Id,cancellation)){throw new ManagementException("Conflict","同じ操作が実行中です。");}
        try{
            this.store.Write(()=>this.store.Put("operation",operation.Id.ToString("N"),operation));
            this.runtime.Publish(draft.Definition,cancellation.Token,publication=>{
                this.PublicationCheckpoint?.Invoke("Prepared",operation.Id);cancellation.Token.ThrowIfCancellationRequested();
                lock(this.store.Gate){var latest=this.GetDraft(user,request.Id,request.DraftRevision);var active=this.store.Read<DefinitionVersion>("active",request.Id);
                    if(latest.Definition.Hash()!=draft.Definition.Hash()||(active?.Version??0)!=preview.CurrentVersion){throw new ManagementException("Conflict","適用準備中に定義が更新されました。");}
                    if(draft.Definition.Kind==DefinitionKind.Database&&this.store.List<DefinitionVersion>("active",predicate:item=>item.Definition.Kind==DefinitionKind.Database&&item.Definition.Database==draft.Definition.Database&&item.Definition.Id!=draft.Definition.Id).Items.Length>0){throw new ManagementException("Conflict","同じDBに別の定義が公開されました。");}
                    if(!this.runtime.Dependencies(draft.Definition).Order(StringComparer.Ordinal).SequenceEqual(preview.Dependencies.Order(StringComparer.Ordinal))){throw new ManagementException("Conflict","依存する定義の版が変更されています。");}
                    foreach(var dependent in publication.Dependents){if(this.store.Read<DefinitionVersion>("active",dependent.Definition.Id)?.Version!=dependent.ExpectedVersion){throw new ManagementException("Conflict","依存する問い合わせが更新されました。");}}
                    var version=new DefinitionVersion(draft.Definition,checked(preview.CurrentVersion+1),user.Name,DateTimeOffset.UtcNow,publication.Primary.ArtifactHash,publication.Primary.StoragePath);operation=operation with{State=OperationState.Succeeded,PublishedVersion=version.Version};
                    this.store.Write(()=>{
                        foreach(var dependent in publication.Dependents){var rebuilt=new DefinitionVersion(dependent.Definition,checked(dependent.ExpectedVersion+1),user.Name,DateTimeOffset.UtcNow,dependent.ArtifactHash,dependent.StoragePath);this.store.Put("version",rebuilt.Definition.Id+":"+rebuilt.Version,rebuilt);this.store.Put("active",rebuilt.Definition.Id,rebuilt);this.store.AddHistory(user.Name,rebuilt.Definition.Database,"Rebuilt",rebuilt.Definition.Id,rebuilt.Version);}
                        this.store.Put("version",request.Id+":"+version.Version,version);this.store.Put("active",request.Id,version);this.store.Put("operation",operation.Id.ToString("N"),operation);this.store.AddHistory(user.Name,draft.Definition.Database,"Applied",request.Id,version.Version);
                    });
                    this.PublicationCheckpoint?.Invoke("Committed",operation.Id);
                }
            });
        }catch(Exception error){
            var durable=this.store.Read<OperationInfo>("operation",operation.Id.ToString("N"));
            if(durable?.State==OperationState.Succeeded){operation=durable;}
            else{var state=OperationState.Failed;if(error is OperationCanceledException){state=OperationState.Cancelled;}operation=operation with{State=state,Error=error.Message};this.store.Write(()=>this.store.Put("operation",operation.Id.ToString("N"),operation));}
        }
        finally{this.running.TryRemove(operation.Id,out _);}
        return operation;
    }
    public OperationInfo GetOperation(UserAccount user,Guid id){
        var operation=this.store.Read<OperationInfo>("operation",id.ToString("N"))??throw new ManagementException("NotFound","操作がありません。");Require(user,operation.Database,ManagementPermission.View);
        if(operation.Owner!=user.Name&&!Can(user,operation.Database,ManagementPermission.Manage)){throw new ManagementException("Permission","他の利用者の操作です。");}
        if(operation.State==OperationState.Running&&!this.running.ContainsKey(id)){operation=operation with{State=OperationState.OutcomeUnknown,Error="再起動前の操作です。公開済みの版と履歴を確認してください。"};this.store.Write(()=>this.store.Put("operation",id.ToString("N"),operation));}return operation;
    }
    public void Cancel(UserAccount user,Guid id){var operation=this.GetOperation(user,id);Require(user,operation.Database,ManagementPermission.Apply);if(this.running.TryGetValue(id,out var source)){source.Cancel();}}
    public ValidationDatabase CreateValidation(UserAccount user,string databaseDefinitionId){var definition=this.GetDefinition(user,databaseDefinitionId);Require(user,definition.Definition.Database,ManagementPermission.Validate);return this.runtime.CreateValidation(definition,user.Name);}
    public ValidationDatabase CreateValidationDraft(UserAccount user,DraftKey key){var draft=this.GetDraft(user,key.Id,key.Revision);Require(user,draft.Definition.Database,ManagementPermission.Validate);var validation=this.runtime.CreateValidation(draft,user.Name);this.RecordValidation(user,draft.Definition,key.Revision,validation.Id,true,null);return validation;}
    public BuildResult Plans(UserAccount user,string id)=>this.runtime.PublishedPlans(this.GetDefinition(user,id));
    public AnalysisResult AnalyzePublished(UserAccount user,string id)=>this.runtime.Analyze(this.GetDefinition(user,id).Definition);
    public void Seed(UserAccount user,SeedRequest request){this.runtime.Seed(request,user);}
    public Page<ValidationDatabase> Validations(UserAccount user,long cursor=0)=>this.store.List<ValidationDatabase>("validation",cursor,predicate:item=>!item.Deleted&&ManagementStore.Normalize(item.Owner)==ManagementStore.Normalize(user.Name)&&Can(user,item.Database,ManagementPermission.Validate));
    public void DropValidation(UserAccount user,string id)=>this.runtime.DropValidation(id,user);
    public IEnumerable<ExecutionRow> Execute(UserAccount user,ExecuteRequest request,CancellationToken cancellationToken){
        Definition definition;if(request.DraftRevision.HasValue){var draft=this.GetDraft(user,request.Id,request.DraftRevision);Require(user,draft.Definition.Database,ManagementPermission.Validate);if(request.ValidationDatabaseId is null){throw new ManagementException("Validation","下書きは検証DBで実行してください。");}definition=draft.Definition;}
        else{var published=this.store.Read<DefinitionVersion>("active",request.Id)??throw new ManagementException("NotFound","公開された問い合わせがありません。");Require(user,published.Definition.Database,ManagementPermission.Execute);definition=published.Definition;}
        return this.ExecuteCore(definition,request,user,cancellationToken);
    }
    private IEnumerable<ExecutionRow> ExecuteCore(Definition definition,ExecuteRequest request,UserAccount user,CancellationToken cancellationToken){
        using var iterator=this.runtime.Execute(definition,request,user,cancellationToken).GetEnumerator();
        while(true){bool more;try{more=iterator.MoveNext();}catch(Exception error){if(request.ValidationDatabaseId is not null&&request.DraftRevision.HasValue){this.RecordValidation(user,definition,request.DraftRevision.Value,request.ValidationDatabaseId,false,error.Message);}throw;}if(!more){break;}yield return iterator.Current;}
        if(request.ValidationDatabaseId is not null&&request.DraftRevision.HasValue){this.RecordValidation(user,definition,request.DraftRevision.Value,request.ValidationDatabaseId,true,null);}
    }
    private void RecordValidation(UserAccount user,Definition definition,long revision,string validation,bool passed,string? error){var result=new ValidationResult(definition.Id,definition.Hash(),revision,user.Name,validation,passed,error,DateTimeOffset.UtcNow);this.store.Write(()=>{this.store.Put("lastValidation",DraftSubject(user.Name,definition.Id),result);var action="ValidationFailed";if(passed){action="ValidationPassed";}this.store.AddHistory(user.Name,definition.Database,action,definition.Id,revision);});}
    public Page<HistoryEntry> History(UserAccount user,long cursor=0)=>this.store.History(cursor,100,entry=>Can(user,entry.Database,ManagementPermission.View));
    public void Dispose(){this.disposed=true;foreach(var item in this.running.Values){item.Cancel();}this.runtime.Dispose();}
    private static string DraftSubject(string owner,string id)=>ManagementStore.Normalize(owner)+":"+id;
    private static string TokenHash(string token)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static void ValidateDefinition(Definition definition){if(string.IsNullOrWhiteSpace(definition.Id)||definition.Id.Length>128||string.IsNullOrWhiteSpace(definition.Database)||definition.Database.Length>128||definition.Id.Any(char.IsControl)||definition.Database.Any(char.IsControl)){throw new ManagementException("Validation","定義ID・DB名が不正です。");}SourceEditor.Validate(definition.Source);}
}
