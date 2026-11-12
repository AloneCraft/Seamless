using System.Collections.Immutable;
using Microsoft.AspNetCore.Components;
using Seamless.Security;

namespace Seamless.Management.Components;

public partial class ロール管理ビュー{
    [Parameter]public bool 表示中か{get;set;}
    [Parameter]public EventCallback<bool> 未保存変更時{get;set;}
    private readonly CancellationTokenSource 寿命=new();
    private readonly List<SecurityResource> DB一覧=[];
    private readonly List<AuthorizationMetadataEntry> メタデータ=[];
    private readonly List<規則入力> 規則=[];
    private readonly List<RoleBinding> 割当=[];
    private Guid? DBID,ロールID,サーバーID;
    private string 名前="",主体認証元="",主体名="",案内="";
    private SecurityPrincipalKind 主体種別;
    private long 版;
    private int ロールページ;
    private bool 取得済みか,処理中か,編集ありか,新規か,エラーか;
    private AuthorizationScopeEdit? 未確定操作;
    private IEnumerable<AuthorizationRoleHeader> 全ロール=>this.メタデータ.Where(項目=>項目.Role is not null).Select(項目=>項目.Role!);
    private IEnumerable<AuthorizationRoleHeader> 表示ロール=>this.全ロール.Skip(this.ロールページ*100).Take(100);
    private IEnumerable<SecurityResource> 資源=>this.メタデータ.Where(項目=>項目.Resource is not null).Select(項目=>項目.Resource!);
    private static readonly SecurityPermission[] 権限=Enum.GetValues<SecurityPermission>().Where(値=>値!=SecurityPermission.None&&((ulong)値&((ulong)値-1))==0).ToArray();
    protected override async Task OnParametersSetAsync(){if(this.表示中か&&!this.取得済みか){await this.一覧取得Async();}}
    public async Task 一覧取得Async(){
        if(this.処理中か){return;}if(this.編集ありか||this.未確定操作 is not null){this.案内=表示文言.取得("編集を保存するか取り消してから再取得してください。");return;}
        await this.実行Async(async()=>{
            var 情報=await this.管理操作.InfoAsync(this.寿命.Token);this.サーバーID=null;
            if(情報.Capabilities.Contains("AuthorizationScopesV1",StringComparer.Ordinal)){this.サーバーID=情報.InstanceId;}
            var 結果=new List<SecurityResource>();long カーソル=0,取得版=0;
            do{var ページ=await this.管理操作.AuthorizationDatabasesAsync(new(取得版,カーソル),this.寿命.Token);取得版=ページ.Version;結果.AddRange(ページ.Items);if(!ページ.HasMore){break;}カーソル=ページ.NextCursor;}while(true);
            if(this.サーバーID is{} サーバー){結果.Add(new(サーバー,null,SecurityResourceKind.Server,"サーバー全体"));}
            this.DB一覧.Clear();this.DB一覧.AddRange(結果);this.取得済みか=true;
            if(this.DBID is{} ID&&結果.Any(DB=>DB.Id==ID)){await this.定義取得Async(ID);}else if(結果.FirstOrDefault() is{} DB){await this.定義取得Async(DB.Id);}else{this.DBID=null;this.メタデータ.Clear();this.ロールID=null;this.案内=表示文言.取得("管理できるDB資源がありません。");}
        });
    }
    private Task DB変更時Async(ChangeEventArgs 引数){
        if(this.編集ありか||this.未確定操作 is not null){this.案内=表示文言.取得("編集を保存するか取り消してからDBを切り替えてください。");return Task.CompletedTask;}
        if(Guid.TryParse(引数.Value?.ToString(),out var ID)){return this.実行Async(()=>this.定義取得Async(ID));}return Task.CompletedTask;
    }
    private async Task 定義取得Async(Guid ID){
        var 結果=new List<AuthorizationMetadataEntry>();long カーソル=0,取得版=0;
        do{
            AuthorizationScopeInfo ページ;
            if(ID==this.サーバーID){ページ=await this.管理操作.AuthorizationScopeAsync(new(ID,取得版,カーソル),this.寿命.Token);}
            else{var DB=await this.管理操作.DatabaseAuthorizationAsync(new(ID,取得版,カーソル),this.寿命.Token);ページ=new(DB.Version,DB.DatabaseId,DB.Items,DB.NextCursor,DB.HasMore);}
            取得版=ページ.Version;結果.AddRange(ページ.Items);if(!ページ.HasMore){break;}カーソル=ページ.NextCursor;
        }while(true);
        this.DBID=ID;this.版=取得版;this.メタデータ.Clear();this.メタデータ.AddRange(結果);this.ロールページ=0;this.ロールID=null;this.規則.Clear();this.割当.Clear();this.名前="";await this.変更通知Async(false);
    }
    private async Task 選択時Async(AuthorizationRoleHeader ロール){
        if(this.編集ありか||this.未確定操作 is not null){this.案内=表示文言.取得("編集を保存するか取り消してからロールを切り替えてください。");return;}
        this.ロールID=ロール.Id;this.名前=ロール.Name;this.新規か=false;this.規則.Clear();this.割当.Clear();
        this.規則.AddRange(this.メタデータ.Where(項目=>項目.Rule?.RoleId==ロール.Id).Select(項目=>new 規則入力(項目.Rule!.Rule)));
        this.割当.AddRange(this.メタデータ.Where(項目=>項目.Binding?.RoleId==ロール.Id).Select(項目=>項目.Binding!));await this.変更通知Async(false);
    }
    private async Task 新規時Async(){
        if(this.DBID is null||this.編集ありか||this.未確定操作 is not null){this.案内=表示文言.取得("現在の編集を保存するか取り消してください。");return;}
        var 許可=SecurityPermission.Read;if(this.DBID==this.サーバーID){許可=SecurityPermission.None;}
        this.ロールID=Guid.NewGuid();this.名前="NewRole";this.新規か=true;this.規則.Clear();this.規則.Add(new(new(this.DBID.Value,許可)));this.割当.Clear();await this.変更通知Async(true);
    }
    private async Task 取消時Async(){
        if(this.未確定操作 is not null){this.案内=表示文言.取得("保存結果を照会してから編集を続けてください。");return;}
        var 元=this.全ロール.FirstOrDefault(ロール=>ロール.Id==this.ロールID);await this.変更通知Async(false);
        if(元 is not null){await this.選択時Async(元);}else{this.ロールID=null;this.規則.Clear();this.割当.Clear();}this.案内="";
    }
    private Task 変更時Async()=>this.変更通知Async(true);
    private Task 変更通知Async(bool 値){this.編集ありか=値;return this.未保存変更時.InvokeAsync(値||this.未確定操作 is not null);}
    private async Task 規則追加時Async(){if(this.DBID is{} ID){this.規則.Add(new(new(ID,SecurityPermission.None)));await this.変更時Async();}}
    private async Task 規則削除時Async(規則入力 入力){this.規則.Remove(入力);await this.変更時Async();}
    private bool サーバー資源か(規則入力 入力)=>this.資源.Any(対象=>対象.Id==入力.資源ID&&対象.Kind==SecurityResourceKind.Server);
    private async Task 資源変更時Async(規則入力 入力){
        if(!this.サーバー資源か(入力)){入力.許可&=~SecurityPermission.ManageServer;入力.拒否&=~SecurityPermission.ManageServer;}
        await this.変更時Async();
    }
    private string 資源表示名(SecurityResource 対象){
        var 名前=new Stack<string>();名前.Push(対象.Name);
        while(対象.ParentId is{} 親ID&&this.資源.FirstOrDefault(値=>値.Id==親ID) is{} 親){名前.Push(親.Name);対象=親;}
        return string.Join(" / ",名前);
    }
    private async Task 権限変更時Async(規則入力 入力,SecurityPermission 権限,bool 拒否か,bool 値){
        if(拒否か){入力.拒否=値?入力.拒否|権限:入力.拒否&~権限;}else{入力.許可=値?入力.許可|権限:入力.許可&~権限;}await this.変更時Async();
    }
    private async Task 割当追加時Async(){
        if(this.ロールID is null||!Guid.TryParse(this.主体認証元,out var 認証元)||認証元==Guid.Empty||string.IsNullOrWhiteSpace(this.主体名)){this.案内=表示文言.取得("認証元のGUIDと主体IDを指定してください。");return;}
        var 値=new RoleBinding(new(認証元,this.主体名),this.主体種別,this.ロールID.Value);
        if(!this.割当.Contains(値)){this.割当.Add(値);await this.変更時Async();}this.主体名="";
    }
    private async Task 割当解除時Async(RoleBinding 値){this.割当.Remove(値);await this.変更時Async();}
    public async Task 保存Async(){
        if(this.処理中か||this.DBID is null||this.ロールID is null||!this.編集ありか||this.未確定操作 is not null){return;}
        var 元=this.メタデータ.Where(項目=>項目.Binding?.RoleId==this.ロールID).Select(項目=>項目.Binding!).ToArray();
        var 要求=new AuthorizationScopeEdit(Guid.NewGuid(),this.DBID.Value,this.版){UpsertRoles=[new(this.ロールID.Value,this.名前,this.規則.Select(入力=>new PermissionRule(入力.資源ID,入力.許可,入力.拒否)).ToImmutableArray())],Assign=this.割当.Except(元).ToArray(),Unassign=元.Except(this.割当).ToArray()};
        this.未確定操作=要求;await this.送信Async();
    }
    private async Task 送信Async(){
        if(this.未確定操作 is not{} 要求||this.処理中か){return;}
        await this.実行Async(async()=>{
            try{
                AuthorizationChangeResult 結果;
                if(要求.ScopeId==this.サーバーID){結果=await this.管理操作.ChangeAuthorizationScopeAsync(要求,this.寿命.Token);}
                else{結果=await this.管理操作.ChangeDatabaseAuthorizationAsync(要求.DatabaseEdit(),this.寿命.Token);}
                await this.保存完了Async(結果);
            }
            catch(ManagementException 例外)when(例外.Code=="Conflict"){
                try{var 結果=await this.管理操作.AuthorizationChangeResultAsync(要求.OperationId,this.寿命.Token);await this.保存完了Async(結果);}
                catch(ManagementException 照会例外)when(照会例外.Code=="NotFound"){this.未確定操作=null;await this.変更時Async();throw 例外;}
            }
            catch(ManagementException 例外)when(例外.Code is "Validation" or "Permission" or "OutsideDatabaseScope" or "UnknownRole" or "UnknownTargetOrOperation"){
                this.未確定操作=null;await this.変更時Async();throw;
            }
        });
    }
    private async Task 削除時Async(){
        if(this.処理中か||this.新規か||this.編集ありか||this.DBID is null||this.ロールID is null||this.未確定操作 is not null){return;}
        this.未確定操作=new(Guid.NewGuid(),this.DBID.Value,this.版){RemoveRoles=[this.ロールID.Value]};await this.変更通知Async(this.編集ありか);await this.送信Async();
    }
    public async Task 結果照会Async(){
        if(this.未確定操作 is not{} 要求||this.処理中か){return;}
        await this.実行Async(async()=>{var 結果=await this.管理操作.AuthorizationChangeResultAsync(要求.OperationId,this.寿命.Token);await this.保存完了Async(結果);});
    }
    private async Task 保存完了Async(AuthorizationChangeResult 結果){
        var 対象=this.未確定操作!.ScopeId;this.未確定操作=null;await this.変更通知Async(false);await this.定義取得Async(対象);this.案内=表示文言.取得("ロールを保存しました。ポリシー v")+結果.Version;
    }
    private async Task 実行Async(Func<Task> 処理){
        if(this.処理中か){return;}this.処理中か=true;this.エラーか=false;this.案内="";
        try{await 処理();}catch(OperationCanceledException)when(this.寿命.IsCancellationRequested){}
        catch(Exception 例外){this.エラーか=true;this.案内=表示文言.取得("操作に失敗しました。編集内容は保持しています。")+例外.Message;}
        finally{this.処理中か=false;if(!this.寿命.IsCancellationRequested){this.StateHasChanged();}}
    }
    private static string 権限名(SecurityPermission 値)=>表示文言.文脈("権限",値 switch{SecurityPermission.Discover=>"一覧",SecurityPermission.Read=>"参照",SecurityPermission.Insert=>"追加",SecurityPermission.Update=>"更新",SecurityPermission.Delete=>"削除",SecurityPermission.Execute=>"実行",SecurityPermission.ReadSource=>"ソース参照",SecurityPermission.Edit=>"設計編集",SecurityPermission.Validate=>"検証",SecurityPermission.Apply=>"適用",SecurityPermission.ManagePermissions=>"権限管理",SecurityPermission.ManageServer=>"サーバー管理",_=>値.ToString()});
    public void Dispose(){this.寿命.Cancel();this.寿命.Dispose();}
    private sealed class 規則入力(PermissionRule 元){public Guid 資源ID=元.ResourceId;public SecurityPermission 許可=元.Allow,拒否=元.Deny;}
}
