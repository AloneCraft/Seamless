using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace Seamless.Management.Server;

internal sealed record ManagementSession(string User,DateTimeOffset ExpiresAt,string CredentialHash);

public sealed partial class ManagementService:IDisposable{
    internal global::Seamless.Execution.ExecutionScope CreateTransportResources()=>new(new(){
        ParentBudget=this.store.ResourceBudget,MemoryLimitBytes=this.store.ResourceBudget.MemoryLimitBytes,TemporaryStorageLimitBytes=0,
        TemporaryDirectory=Path.Combine(this.store.Root,"temporary","ipc")
    });
    private readonly ManagementStore store;
    private readonly NativeRuntime runtime;
    private readonly ConcurrentDictionary<string,ManagementSession> sessions=new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string,(int Failures,DateTimeOffset Until)> failures=new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid,CancellationTokenSource> running=new();
    private readonly PasswordHasher<UserAccount> hasher=new();
    private bool disposed;
    internal Action<string,Guid>? PublicationCheckpoint{get;set;}
    internal Action? BeforeQueryAdmission{get=>this.runtime.BeforeQueryAdmission;set=>this.runtime.BeforeQueryAdmission=value;}
    internal Action? BeforeQueryExecution{get=>this.runtime.BeforeQueryExecution;set=>this.runtime.BeforeQueryExecution=value;}
    internal Action? AfterQueryRow{get=>this.runtime.AfterQueryRow;set=>this.runtime.AfterQueryRow=value;}
    internal WeakReference? LastQueryContext=>this.runtime.LastQueryContext;
    public ManagementService(ManagementStore store):this(store,null){}
    public ManagementService(ManagementStore store,ReadAuditOptions? readAudit):this(store,readAudit,true){}
    internal ManagementService(ManagementStore store,ReadAuditOptions? readAudit,bool startAuditWorker,bool startReadAuditWorker=true){readAudit?.Validate();if(readAudit is not null&&store.Format<5){throw new ManagementException("MigrationRequired","通常読み取りの集計はupgrade-auditで形式5へ明示更新してから設定してください。");}this.store=store;this.runtime=new(store,startAuditWorker);this.InitializeExecutionHistory();if(readAudit is not null){this.readAuditBuffer=new(readAudit,(id,rows,cancellation)=>{cancellation.ThrowIfCancellationRequested();this.ReadAuditPersisting?.Invoke(Environment.CurrentManagedThreadId);store.ApplyReadSummaryBatch(id,rows);return ValueTask.CompletedTask;},startWorker:startReadAuditWorker,flushOnDispose:startReadAuditWorker);this.runtime.ReadCompleted=sample=>{var ticks=sample.At.UtcTicks;var start=ticks-ticks%readAudit.Window.Ticks;this.readAuditBuffer.Record(new(new(start,TimeSpan.Zero),new(start+readAudit.Window.Ticks,TimeSpan.Zero),store.InstanceId,sample.SubjectId,sample.Database,sample.QueryId,sample.CodeVersion,null));};}}
    public ServerInfo Info{get{string[] capabilities=["NativeManagementV1","StaticPlanV1","PersonalDrafts","ValidationDatabase","ExplicitApply","DefinitionInfoV1","ProtectedSourceV1","NativeCatalogV1","NativeForeignKeysV1","SchemaMigrationsV1","TablePublicationV1","TableRowsV1","UserProfilesV1","DatabaseAuthorizationV1","AuthorizationScopesV1","DatabaseRenameV1","DraftPublishedVersionGuardV1","QueryTraceV1","CommonFunctionsV1","ResourceHeatV1","ResourceHeatHistoryV1"];if(this.serverSettingsHost is not null){capabilities=[..capabilities,"ServerSettingsV1"];}if(this.store.HasAudit){capabilities=[..capabilities,"AuditSearchV1","AuditDeliveryV1","AuditReadMethodsV1"];}if(this.store.Format>=5){capabilities=[..capabilities,"AuditReadBufferV1"];}return new(this.store.InstanceId,"1",System.Runtime.InteropServices.RuntimeInformation.OSDescription,capabilities);}}
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
    private UserAccount CurrentActor(UserAccount user){
        // 組み込み・OS所有者IPCの主体は、同名のアプリ利用者とは独立した権限を持つ。
        var actor=user;
        if(user.PasswordHash.Length>0){actor=this.store.Read<UserAccount>("user",ManagementStore.Normalize(user.Name))??throw new ManagementException("Authentication","利用者が存在しません。");}
        if(actor.Disabled){throw new ManagementException("Authentication","利用者が無効です。");}
        return actor;
    }
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
        var definition=request.Definition.Freeze();ValidateDefinition(definition);Require(user,this.store.CurrentDatabase(definition),ManagementPermission.Edit);
        lock(this.store.Gate){
            var actor=this.CurrentActor(user);
            Require(actor,this.store.CurrentDatabase(definition),ManagementPermission.Edit);
            var key=DraftSubject(user.Name,definition.Id);var previous=this.store.Read<Draft>("draft",key);
            if((previous?.Revision??0)!=request.ExpectedRevision){throw new ManagementException("Conflict","下書きが別の操作で更新されました。");}
            var active=this.store.Read<DefinitionVersion>("active",definition.Id);
            if(request.ExpectedPublishedVersion is{} version&&(active?.Version!=version||request.BaseVersion!=version)){
                throw new ManagementException("Conflict","確認後に稼働版が変わりました。差分を開き直してください。");
            }
            if(active is null&&definition.Kind==DefinitionKind.Database&&definition.Database.Equals("system",StringComparison.OrdinalIgnoreCase)){throw new ManagementException("Validation","systemはシステムDB名です。");}
            if(active is not null&&(active.Definition.Database!=definition.Database||active.Definition.Kind!=definition.Kind)){throw new ManagementException("Conflict","既存の定義のDB・種類は変更できません。");}
            if(definition.Kind==DefinitionKind.Database&&this.store.List<DefinitionVersion>("active",predicate:item=>item.Definition.Kind==DefinitionKind.Database&&item.Database==this.store.CurrentDatabase(definition)&&item.Definition.Id!=definition.Id).Items.Length>0){throw new ManagementException("Conflict","このDBには既に別のDB定義があります。");}
            var draft=new Draft(definition,user.Name,checked(request.ExpectedRevision+1),request.BaseVersion,DateTimeOffset.UtcNow);
            this.store.Write(()=>{this.store.Put("draft",key,draft);this.store.AddHistory(user.Name,this.store.CurrentDatabase(definition),"DraftSaved",definition.Id,draft.Revision);});return draft;
        }
    }
    public Draft? FindDraft(UserAccount user,string id){
        var draft=this.store.Read<Draft>("draft",DraftSubject(user.Name,id));
        if(draft is not null){Require(user,this.store.CurrentDatabase(draft.Definition),ManagementPermission.Edit);}
        return draft;
    }
    public Draft GetDraft(UserAccount user,string id,long? revision=null){
        var draft=this.FindDraft(user,id)??throw new ManagementException("NotFound","個人の下書きがありません。");
        if(revision.HasValue&&draft.Revision!=revision){throw new ManagementException("Conflict","下書きの版が変わっています。");}return draft;
    }
    public Page<DefinitionSummary> Definitions(UserAccount user,long cursor=0)=>this.store.SelectPage<DefinitionVersion,DefinitionSummary>("active",cursor,100,item=>Can(user,this.store.CurrentDatabase(item.Definition),ManagementPermission.View),item=>new(new(item.Definition.Id,this.store.CurrentDatabase(item.Definition),item.Definition.Kind,item.Definition.EntryType,item.Definition.EntryMember,item.Definition.WritesData),item.Version,item.Owner,item.CreatedAt,item.ArtifactHash));
    public Page<DraftSummary> Drafts(UserAccount user,long cursor=0)=>this.store.SelectPage<Draft,DraftSummary>("draft",cursor,100,item=>ManagementStore.Normalize(item.Owner)==ManagementStore.Normalize(user.Name)&&Can(user,this.store.CurrentDatabase(item.Definition),ManagementPermission.Edit),item=>new(new(item.Definition.Id,this.store.CurrentDatabase(item.Definition),item.Definition.Kind,item.Definition.EntryType,item.Definition.EntryMember,item.Definition.WritesData),item.Revision,item.BaseVersion,item.SavedAt));
    public DefinitionVersion GetDefinition(UserAccount user,string id){var definition=this.Published(id);RequireSource(user,definition);return definition;}
    public BuildResult Build(UserAccount user,DraftKey key){var draft=this.GetDraft(user,key.Id,key.Revision);Require(user,this.store.CurrentDatabase(draft.Definition),ManagementPermission.Validate);using var compiled=this.ReadDraftBuild(user,draft,key);return compiled.Result;}
    public AnalysisResult Analyze(UserAccount user,Definition definition){ValidateDefinition(definition);Require(user,this.store.CurrentDatabase(definition),ManagementPermission.Edit|ManagementPermission.Validate);return this.runtime.Analyze(definition);}
    public DefinitionVersion Version(UserAccount user,string id,long version){var value=this.store.Read<DefinitionVersion>("version",id+":"+version)??throw new ManagementException("NotFound","指定した版がありません。");RequireSource(user,value);return value;}
    internal void RequestStop(){foreach(var item in this.running.Values){item.Cancel();}}
    public ChangePreview Preview(UserAccount user,DraftKey key){
        var draft=this.GetDraft(user,key.Id,key.Revision);Require(user,this.store.CurrentDatabase(draft.Definition),ManagementPermission.Apply);
        return this.PreviewPublication(user,draft,this.store.Read<DefinitionVersion>("active",key.Id),draft.Definition,null,key.BundleHash);
    }
    public TableChangePreview PreviewTable(UserAccount user,TableDraftKey key){
        if(key.Draft is null||string.IsNullOrWhiteSpace(key.Table)){throw new ManagementException("InvalidRequest","下書きと対象の表を指定してください。");}
        var draft=this.GetDraft(user,key.Draft.Id,key.Draft.Revision);
        Require(user,this.store.CurrentDatabase(draft.Definition),ManagementPermission.Apply);
        RequireTableSourcePreview(key.Draft.BundleHash);
        var active=this.store.Read<DefinitionVersion>("active",draft.Definition.Id);
        var plan=PrepareTable(draft,active,key.Table);
        return new(this.PreviewPublication(user,draft,active,plan.Candidate,key.Table),plan.Tables);
    }
    private static TablePublication PrepareTable(Draft draft,DefinitionVersion? active,string table){
        if(active is null||active.Version!=draft.BaseVersion){throw new ManagementException("Conflict","稼働版を確認し、下書きの基準版を更新してください。");}
        return DatabaseDesigner.PrepareTablePublication(active.Definition,draft.Definition,table);
    }
    private ChangePreview PreviewPublication(UserAccount user,Draft draft,DefinitionVersion? active,Definition candidate,string? table,string? bundleHash=null){
        using var build=this.ReadDraftBuild(user,draft with{Definition=candidate},new(draft.Definition.Id,draft.Revision){BundleHash=bundleHash});string[] changes;DiagnosticInfo? migrationError=null;
        try{changes=this.runtime.DescribeChanges(active,candidate,build);}catch(ManagementException error){changes=[error.Message];migrationError=new(error.Code,error.Message);}
        var dependencies=this.runtime.Dependencies(candidate);var version=active?.Version??0;
        var diagnostics=build.Result.Diagnostics.Concat(this.runtime.ValidateDependents(candidate,build)).ToList();if(migrationError is not null){diagnostics.Add(migrationError);}if(version!=draft.BaseVersion){diagnostics.Add(new("Conflict","共有版が進んでいます。差分を確認し基準版を更新してください。"));}
        var validation=this.store.Read<ValidationResult>("lastValidation",DraftSubject(user.Name,draft.Definition.Id));if(validation is null||validation.DefinitionHash!=candidate.Hash()||validation.DraftRevision!=draft.Revision||validation.BundleHash!=bundleHash){validation=null;diagnostics.Add(new("ValidationNotRun","この適用候補の検証結果はまだありません。",Severity:"Warning"));}else if(!validation.Passed){diagnostics.Add(new("ValidationFailed","直近の検証は失敗しています: "+validation.Error,Severity:"Warning"));}
        var intent=draft.Definition.Hash()+"\n"+draft.Revision+"\n"+version+"\n"+string.Join("\n",dependencies);
        if(table is not null){intent+="\nTablePublicationV1\n"+table+"\n"+candidate.Hash();}
        if(bundleHash is not null){intent+="\nSourceBuildV1\n"+bundleHash;}
        var token=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(intent)));
        return new(token,draft.Definition.Id,draft.Revision,version,changes,dependencies,diagnostics.All(item=>item.Severity!="Error"),diagnostics.ToArray(),validation,candidate.Migration?.HasDataLoss==true){BundleHash=bundleHash};
    }
    public OperationInfo Apply(UserAccount user,ApplyRequest request)=>this.ApplyPublication(user,request,null);
    public OperationInfo ApplyTable(UserAccount user,ApplyTableRequest request){
        if(request.Apply is null||string.IsNullOrWhiteSpace(request.Table)){throw new ManagementException("InvalidRequest","適用要求と対象の表を指定してください。");}
        return this.ApplyPublication(user,request.Apply,request.Table);
    }
    private OperationInfo ApplyPublication(UserAccount user,ApplyRequest request,string? table){
        var payload=System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(request,ManagementJson.Options);
        if(table is not null){payload=System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new ApplyTableRequest(request,table),ManagementJson.Options);}
        var intent=Convert.ToHexString(SHA256.HashData(payload));
        lock(this.store.Gate){var existing=this.store.Read<OperationInfo>("operation",request.OperationId.ToString("N"));if(existing is not null){Require(user,existing.Database,ManagementPermission.Apply);if(existing.Owner!=user.Name||existing.IntentHash!=intent){throw new ManagementException("Conflict","操作IDは別の要求に使用されています。");}return existing;}}
        var draft=this.GetDraft(user,request.Id,request.DraftRevision);Require(user,this.store.CurrentDatabase(draft.Definition),ManagementPermission.Apply);
        lock(this.store.Gate){var existing=this.store.Read<OperationInfo>("operation",request.OperationId.ToString("N"));if(existing is not null){if(existing.Owner!=user.Name||existing.IntentHash!=intent){throw new ManagementException("Conflict","操作IDは別の要求に使用されています。");}return existing;}}
        var baseline=this.store.Read<DefinitionVersion>("active",request.Id);
        TablePublication? plan=null;
        if(table is not null){RequireTableSourcePreview(request.BundleHash);plan=PrepareTable(draft,baseline,table);}
        var candidate=plan?.Candidate??draft.Definition;
        var preview=this.PreviewPublication(user,draft,baseline,candidate,table,request.BundleHash);if(!preview.CanApply||preview.Token!=request.PreviewToken){throw new ManagementException("Conflict","変更影響を再確認してください。");}
        if(preview.RequiresBackup&&!request.ConfirmDataLoss){throw new ManagementException("BackupRequired","削除を含む変更にはバックアップとデータ削除の明示確認が必要です。");}
        string? backup=null;if(preview.RequiresBackup){backup=Path.Combine(this.store.Root,"backups",request.OperationId.ToString("N"));}
        var operation=new OperationInfo(request.OperationId,user.Name,this.store.CurrentDatabase(draft.Definition),request.Id,OperationState.Running,null,null,DateTimeOffset.UtcNow,intent,backup);
        using var cancellation=new CancellationTokenSource();if(!this.running.TryAdd(operation.Id,cancellation)){throw new ManagementException("Conflict","同じ操作が実行中です。");}
        CompiledSource? selected=null;
        try{
            this.store.Write(()=>this.store.Put("operation",operation.Id.ToString("N"),operation));
            if(request.BundleHash is not null){selected=this.ReadDraftBuild(user,draft,new(request.Id,request.DraftRevision){BundleHash=request.BundleHash});}
            this.runtime.Publish(candidate,cancellation.Token,publication=>{
                this.PublicationCheckpoint?.Invoke("Prepared",operation.Id);cancellation.Token.ThrowIfCancellationRequested();
                lock(this.store.Gate){var latest=this.GetDraft(user,request.Id,request.DraftRevision);var active=this.store.Read<DefinitionVersion>("active",request.Id);
                    if(latest.Definition.Hash()!=draft.Definition.Hash()||(active?.Version??0)!=preview.CurrentVersion){throw new ManagementException("Conflict","適用準備中に定義が更新されました。");}
                    if(request.BundleHash is not null&&this.GetImportedSourceBuild(user,new(request.Id,request.DraftRevision)).Build.BundleHash!=request.BundleHash){throw new ManagementException("Conflict","適用準備中に選択済み成果物が変更されています。");}
                    if(draft.Definition.Kind==DefinitionKind.Database&&this.store.List<DefinitionVersion>("active",predicate:item=>item.Definition.Kind==DefinitionKind.Database&&item.Database==publication.Primary.Database&&item.Definition.Id!=draft.Definition.Id).Items.Length>0){throw new ManagementException("Conflict","同じDBに別の定義が公開されました。");}
                    if(!this.runtime.Dependencies(candidate).Order(StringComparer.Ordinal).SequenceEqual(preview.Dependencies.Order(StringComparer.Ordinal))){throw new ManagementException("Conflict","依存する定義の版が変更されています。");}
                    foreach(var dependent in publication.Dependents){if(this.store.Read<DefinitionVersion>("active",dependent.Definition.Id)?.Version!=dependent.ExpectedVersion){throw new ManagementException("Conflict","依存する問い合わせが更新されました。");}}
                    var version=new DefinitionVersion(candidate,checked(preview.CurrentVersion+1),user.Name,DateTimeOffset.UtcNow,publication.Primary.ArtifactHash,publication.Primary.StoragePath){Database=publication.Primary.Database};operation=operation with{State=OperationState.Succeeded,PublishedVersion=version.Version};
                    this.store.Write(()=>{
                        if(publication.Primary.AuditOrigin is{} origin){this.store.Put("auditOrigin",origin.WalId.ToString("N"),origin);}
                        foreach(var dependent in publication.Dependents){var rebuilt=new DefinitionVersion(dependent.Definition,checked(dependent.ExpectedVersion+1),user.Name,DateTimeOffset.UtcNow,dependent.ArtifactHash,dependent.StoragePath){Database=dependent.Database};this.store.Put("version",rebuilt.Definition.Id+":"+rebuilt.Version,rebuilt);this.store.Put("active",rebuilt.Definition.Id,rebuilt);this.store.AddHistory(user.Name,rebuilt.Database,"Rebuilt",rebuilt.Definition.Id,rebuilt.Version);}
                        this.store.Put("version",request.Id+":"+version.Version,version);this.store.Put("active",request.Id,version);this.store.Put("operation",operation.Id.ToString("N"),operation);this.store.AddHistory(user.Name,this.store.CurrentDatabase(draft.Definition),"Applied",request.Id,version.Version);
                        if(plan is not null){
                            var remaining=draft with{Definition=plan.RemainingDraft,BaseVersion=version.Version,Revision=checked(draft.Revision+1),SavedAt=version.CreatedAt};
                            this.store.Put("draft",DraftSubject(user.Name,request.Id),remaining);
                        }
                    });
                    this.PublicationCheckpoint?.Invoke("Committed",operation.Id);
                }
            },backup,selected:selected);
        }catch(Exception error){
            var durable=this.store.Read<OperationInfo>("operation",operation.Id.ToString("N"));
            if(durable?.State==OperationState.Succeeded){operation=durable;}
            else{var state=OperationState.Failed;if(error is OperationCanceledException){state=OperationState.Cancelled;}operation=operation with{State=state,Error=error.GetBaseException().Message};this.store.Write(()=>this.store.Put("operation",operation.Id.ToString("N"),operation));}
        }
        finally{selected?.Dispose();this.running.TryRemove(operation.Id,out _);}
        return operation;
    }
    public OperationInfo GetOperation(UserAccount user,Guid id){
        var operation=this.store.Read<OperationInfo>("operation",id.ToString("N"))??throw new ManagementException("NotFound","操作がありません。");Require(user,operation.Database,ManagementPermission.View);
        if(operation.Owner!=user.Name&&!Can(user,operation.Database,ManagementPermission.Manage)){throw new ManagementException("Permission","他の利用者の操作です。");}
        if(operation.State==OperationState.Running&&!this.running.ContainsKey(id)){operation=operation with{State=OperationState.OutcomeUnknown,Error="再起動前の操作です。公開済みの版と履歴を確認してください。"};this.store.Write(()=>this.store.Put("operation",id.ToString("N"),operation));}return operation;
    }
    public void Cancel(UserAccount user,Guid id){var operation=this.GetOperation(user,id);Require(user,operation.Database,ManagementPermission.Apply);if(this.running.TryGetValue(id,out var source)){source.Cancel();}}
    public ValidationDatabase CreateValidation(UserAccount user,string databaseDefinitionId){var definition=this.Published(databaseDefinitionId);Require(user,definition.Database,ManagementPermission.View);Require(user,definition.Database,ManagementPermission.Validate);return this.runtime.CreateValidation(definition,user.Name);}
    public ValidationDatabase CreateValidationDraft(UserAccount user,DraftKey key){var draft=this.GetDraft(user,key.Id,key.Revision);Require(user,this.store.CurrentDatabase(draft.Definition),ManagementPermission.Validate);using var compiled=this.ReadDraftBuild(user,draft,key);var validation=this.runtime.CreateValidation(draft,user.Name,compiled);this.RecordValidation(user,draft.Definition,key.Revision,validation.Id,true,null,validation.BundleHash);return validation;}
    public BuildResult Plans(UserAccount user,string id){
        var result=this.runtime.PublishedPlans(this.GetDefinition(user,id));
        return result with{Plans=result.Plans.Select(plan=>plan with{GeneratedCode=null,ProfiledGeneratedCode=null}).ToArray()};
    }
    public AnalysisResult AnalyzePublished(UserAccount user,string id)=>this.runtime.Analyze(this.GetDefinition(user,id).Definition);
    public void Seed(UserAccount user,SeedRequest request){this.runtime.Seed(request,user);}
    public void SeedBatch(UserAccount user,SeedBatchRequest request){this.runtime.SeedBatch(request,user);}
    public Page<ValidationDatabase> Validations(UserAccount user,long cursor=0)=>this.store.List<ValidationDatabase>("validation",cursor,predicate:item=>!item.Deleted&&ManagementStore.Normalize(item.Owner)==ManagementStore.Normalize(user.Name)&&Can(user,item.Database,ManagementPermission.Validate));
    public void DropValidation(UserAccount user,string id)=>this.runtime.DropValidation(id,user);
    public IEnumerable<ExecutionRow> Execute(UserAccount user,ExecuteRequest request,CancellationToken cancellationToken){
        return this.Execute(user,request,cancellationToken,null);
    }
    internal IEnumerable<ExecutionRow> Execute(UserAccount user,ExecuteRequest request,CancellationToken cancellationToken,Action? beforeCompletion){
        Definition definition;if(request.DraftRevision.HasValue){var draft=this.GetDraft(user,request.Id,request.DraftRevision);Require(user,this.store.CurrentDatabase(draft.Definition),ManagementPermission.Validate);if(request.ValidationDatabaseId is null){throw new ManagementException("Validation","下書きは検証DBで実行してください。");}definition=draft.Definition;}
        else{var published=this.store.Read<DefinitionVersion>("active",request.Id)??throw new ManagementException("NotFound","公開された問い合わせがありません。");Require(user,published.Database,ManagementPermission.Execute);definition=published.Definition;}
        return this.ExecuteCore(definition,request,user,cancellationToken,beforeCompletion);
    }
    private IEnumerable<ExecutionRow> ExecuteCore(Definition definition,ExecuteRequest request,UserAccount user,CancellationToken cancellationToken,Action? beforeCompletion){
        using var iterator=this.runtime.Execute(definition,request,user,cancellationToken,beforeCompletion).GetEnumerator();
        while(true){bool more;try{more=iterator.MoveNext();}catch(Exception error){if(request.ValidationDatabaseId is not null&&request.DraftRevision.HasValue){this.RecordValidation(user,definition,request.DraftRevision.Value,request.ValidationDatabaseId,false,error.Message);}throw;}if(!more){break;}yield return iterator.Current;}
        if(request.ValidationDatabaseId is not null&&request.DraftRevision.HasValue){this.RecordValidation(user,definition,request.DraftRevision.Value,request.ValidationDatabaseId,true,null);}
    }
    private void RecordValidation(UserAccount user,Definition definition,long revision,string validation,bool passed,string? error,string? bundleHash=null){var result=new ValidationResult(definition.Id,definition.Hash(),revision,user.Name,validation,passed,error,DateTimeOffset.UtcNow){BundleHash=bundleHash};this.store.Write(()=>{this.store.Put("lastValidation",DraftSubject(user.Name,definition.Id),result);var action="ValidationFailed";if(passed){action="ValidationPassed";}var database=this.store.Read<ValidationDatabase>("validation",validation)?.Database??this.store.CurrentDatabase(definition);this.store.AddHistory(user.Name,database,action,definition.Id,revision);});}
    public Page<HistoryEntry> History(UserAccount user,long cursor=0)=>this.store.History(cursor,100,entry=>{
        if(entry.DatabaseDefinitionId is null){return Can(user,entry.Database,ManagementPermission.View);}
        var database=this.store.Read<DefinitionVersion>("active",entry.DatabaseDefinitionId);
        if(database is null){return Can(user,"*",ManagementPermission.View);}
        return Can(user,database.Database,ManagementPermission.View);
    });
    public void Dispose(){if(this.disposed){return;}this.disposed=true;foreach(var item in this.running.Values){item.Cancel();}try{this.runtime.Dispose();}finally{try{this.DisposeCommonAuthorization();}finally{try{if(this.readAuditBuffer is not null){this.readAuditBuffer.DisposeAsync().AsTask().GetAwaiter().GetResult();}}finally{this.DisposeExecutionHistoryAsync().AsTask().GetAwaiter().GetResult();}}}}
    private static string DraftSubject(string owner,string id)=>ManagementStore.Normalize(owner)+":"+id;
    private static string TokenHash(string token)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static void ValidateDefinition(Definition definition){if(string.IsNullOrWhiteSpace(definition.Id)||definition.Id.Length>128||string.IsNullOrWhiteSpace(definition.Database)||definition.Database.Length>128||definition.Id.Any(char.IsControl)||definition.Database.Any(char.IsControl)){throw new ManagementException("Validation","定義ID・DB名が不正です。");}SourceEditor.Validate(definition.Source);}
}
